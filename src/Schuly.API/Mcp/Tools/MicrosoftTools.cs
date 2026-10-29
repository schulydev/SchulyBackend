using System.ComponentModel;
using System.Text.Json;
using Mediator;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Schuly.Application.Queries.LinkedAccount;
using Schuly.Domain.Mcp;
using Schuly.Plugin.Abstractions;
using static Schuly.API.Mcp.Tools.McpToolSupport;

namespace Schuly.API.Mcp.Tools
{
    /// <summary>
    /// Microsoft 365 through the Microsoft plugin's own endpoints, called in process with the caller's principal, so the plugin's ownership checks and
    /// sign-in handling apply exactly as for the web app. Every tool takes an optional account_id and is limited to the accounts the connection may use.
    /// </summary>
    [McpServerToolType]
    public sealed class MicrosoftTools(IMediator mediator, IHttpContextAccessor accessor, InProcessPluginApi plugins, IHttpClientFactory httpClientFactory)
    {
        private const string Base = "/api/plugins/microsoft";
        private const int MaxDriveFileBytes = 1024 * 1024;
        private const int MaxBase64Bytes = 256 * 1024;

        // Graph's pre-authenticated download links point here. The URL comes from Microsoft's answer, never from the model, and is still checked so a fetch can only reach OneDrive/SharePoint.
        private static readonly string[] DownloadHosts = [".sharepoint.com", ".1drv.com", ".onedrive.com", ".livefilestore.com", ".sharepoint-df.com"];

        [McpServerTool(Name = "list_mail_folders", Title = "List mail folders", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
        [Description("Lists the mail folders of a linked Microsoft 365 account with ids and unread counts. Use a folder id or a well-known name (inbox, sentitems, drafts, deleteditems, archive, junkemail) with list_mail and move_mail.")]
        [McpAccess(McpArea.Mail, McpAccessLevel.Read)]
        public async Task<string> ListMailFolders([Description("Linked Microsoft account; defaults to the first one this connection may use.")] Guid? accountId = null, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            var data = await ReadAsync($"{Base}/accounts/{account}/mail/folders", ct);
            return Serialize(new { accountId = account, folders = Clean(data ?? default) });
        }

        [McpServerTool(Name = "list_mail", Title = "List or search mail", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
        [Description("Lists messages in a mail folder, newest first, with sender, subject, date, read state and a short preview. With search, runs a Microsoft 365 mail search instead (offset is then not supported). Read a full message with read_mail.")]
        [McpAccess(McpArea.Mail, McpAccessLevel.Read)]
        public async Task<string> ListMail(Guid? accountId = null, [Description("Folder id or well-known name. Default inbox.")] string folder = "inbox", [Description("Only unread messages.")] bool unreadOnly = false, [Description("Search text (KQL), for example from:anna subject:exam.")] string? search = null, [Description("Messages to skip, for paging. Not with search.")] int offset = 0, [Description("Messages per page, 1-50.")] int limit = 20, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            limit = Math.Clamp(limit, 1, 50);
            var query = $"folder={Uri.EscapeDataString(folder)}&top={limit}&skip={(string.IsNullOrWhiteSpace(search) ? Math.Max(0, offset) : 0)}&unread={unreadOnly.ToString().ToLowerInvariant()}" + (string.IsNullOrWhiteSpace(search) ? "" : $"&search={Uri.EscapeDataString(search)}");
            var messages = Items(await ReadAsync($"{Base}/accounts/{account}/mail?{query}", ct)).Select(MailSummary).ToList();
            return Serialize(new { accountId = account, folder, offset, messages, nextOffset = messages.Count == limit && string.IsNullOrWhiteSpace(search) ? offset + limit : (int?)null });
        }

        [McpServerTool(Name = "read_mail", Title = "Read mail", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
        [Description("Reads one message: sender, recipients, date, attachment names and the body as plain text. Long bodies are paged: call again with the returned nextOffset.")]
        [McpAccess(McpArea.Mail, McpAccessLevel.Read)]
        public async Task<string> ReadMail([Description("Message id from list_mail.")] string messageId, Guid? accountId = null, [Description("Character offset into the body.")] int offset = 0, [Description("Body characters per page.")] int maxChars = DefaultMaxChars, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            var message = await ReadAsync($"{Base}/accounts/{account}/mail/{Uri.EscapeDataString(messageId)}", ct) ?? default;
            List<string>? attachments = null;
            if (Bool(message, "hasAttachments") == true)
            {
                var list = await plugins.GetAsync($"{Base}/accounts/{account}/mail/{Uri.EscapeDataString(messageId)}/attachments", ct);
                attachments = list.IsSuccess ? Items(list.Json()).Select(a => $"{Str(a, "name")} ({Long(a, "size") ?? 0} bytes)").ToList() : null;
            }

            return Serialize(new
            {
                accountId = account,
                id = Str(message, "id"),
                subject = Str(message, "subject"),
                from = Address(Prop(message, "from")),
                to = Addresses(message, "toRecipients"),
                cc = Addresses(message, "ccRecipients"),
                received = Str(message, "receivedDateTime"),
                isRead = Bool(message, "isRead"),
                flag = Prop(message, "flag") is { } f ? Str(f, "flagStatus") : null,
                attachments,
                webLink = Str(message, "webLink"),
                body = TextPage(BodyText(Prop(message, "body")), offset, maxChars),
            });
        }

        [McpServerTool(Name = "send_mail", Title = "Send mail", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
        [Description("Sends a new email from the user's Microsoft 365 account right away. It cannot be unsent, so confirm recipients and content with the user first.")]
        [McpAccess(McpArea.Mail, McpAccessLevel.ReadWrite)]
        public async Task<string> SendMail([Description("Recipient addresses.")] string[] to, [Description("Subject line.")] string subject, [Description("Plain-text body.")] string body, [Description("CC addresses.")] string[]? cc = null, [Description("BCC addresses.")] string[]? bcc = null, Guid? accountId = null, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            await WriteAsync(HttpMethods.Post, $"{Base}/accounts/{account}/mail/send", new { to, cc, bcc, subject, body, html = false }, ct);
            return Serialize(new { sent = true, accountId = account, to, subject });
        }

        [McpServerTool(Name = "reply_mail", Title = "Reply to mail", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
        [Description("Replies to a message (or replies to all) with the given text. Sends immediately.")]
        [McpAccess(McpArea.Mail, McpAccessLevel.ReadWrite)]
        public async Task<string> ReplyMail(string messageId, [Description("The reply text.")] string comment, [Description("Reply to all recipients.")] bool replyAll = false, Guid? accountId = null, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            await WriteAsync(HttpMethods.Post, $"{Base}/accounts/{account}/mail/{Uri.EscapeDataString(messageId)}/reply", new { comment, replyAll }, ct);
            return Serialize(new { replied = true, messageId, replyAll });
        }

        [McpServerTool(Name = "forward_mail", Title = "Forward mail", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
        [Description("Forwards a message to new recipients with an optional note. Sends immediately.")]
        [McpAccess(McpArea.Mail, McpAccessLevel.ReadWrite)]
        public async Task<string> ForwardMail(string messageId, [Description("Recipient addresses.")] string[] to, [Description("Note above the forwarded message.")] string? comment = null, Guid? accountId = null, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            await WriteAsync(HttpMethods.Post, $"{Base}/accounts/{account}/mail/{Uri.EscapeDataString(messageId)}/forward", new { to, comment = comment ?? string.Empty }, ct);
            return Serialize(new { forwarded = true, messageId, to });
        }

        [McpServerTool(Name = "move_mail", Title = "Move mail", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = true)]
        [Description("Moves a message to another folder (folder id or well-known name such as archive or deleteditems). Returns the message's new id.")]
        [McpAccess(McpArea.Mail, McpAccessLevel.ReadWrite)]
        public async Task<string> MoveMail(string messageId, [Description("Target folder id or well-known name.")] string folder, Guid? accountId = null, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            var result = await WriteAsync(HttpMethods.Post, $"{Base}/accounts/{account}/mail/{Uri.EscapeDataString(messageId)}/move", new { folder }, ct);
            return Serialize(new { moved = true, folder, newMessageId = result is { } r ? Str(r, "id") : null });
        }

        [McpServerTool(Name = "flag_mail", Title = "Flag mail", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = true)]
        [Description("Sets a message's follow-up flag: flagged, complete or notFlagged.")]
        [McpAccess(McpArea.Mail, McpAccessLevel.ReadWrite)]
        public async Task<string> FlagMail(string messageId, [Description("flagged, complete or notFlagged.")] string status, Guid? accountId = null, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            await WriteAsync(HttpMethods.Post, $"{Base}/accounts/{account}/mail/{Uri.EscapeDataString(messageId)}/flag", new { status }, ct);
            return Serialize(new { flagged = status, messageId });
        }

        [McpServerTool(Name = "mark_mail_read", Title = "Mark mail read or unread", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = true)]
        [Description("Marks a message as read or unread.")]
        [McpAccess(McpArea.Mail, McpAccessLevel.ReadWrite)]
        public async Task<string> MarkMailRead(string messageId, bool read = true, Guid? accountId = null, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            await WriteAsync(HttpMethods.Post, $"{Base}/accounts/{account}/mail/{Uri.EscapeDataString(messageId)}/read", new { read }, ct);
            return Serialize(new { messageId, isRead = read });
        }

        [McpServerTool(Name = "delete_mail", Title = "Delete mail", ReadOnly = false, Destructive = true, Idempotent = true, OpenWorld = true)]
        [Description("Deletes a message (Microsoft 365 moves it to Deleted Items). Needs the connection's permission for destructive actions.")]
        [McpAccess(McpArea.Mail, McpAccessLevel.ReadWrite)]
        [McpDestructive]
        public async Task<string> DeleteMail(string messageId, Guid? accountId = null, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            await WriteAsync(HttpMethods.Delete, $"{Base}/accounts/{account}/mail/{Uri.EscapeDataString(messageId)}", null, ct);
            return Serialize(new { deleted = true, messageId });
        }

        [McpServerTool(Name = "list_chats", Title = "List Teams chats", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
        [Description("Lists the user's Microsoft Teams chats, most recent first, with topic, members and the last message preview.")]
        [McpAccess(McpArea.Teams, McpAccessLevel.Read)]
        public async Task<string> ListChats(Guid? accountId = null, [Description("Chats to return, 1-50.")] int limit = 20, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            var chats = Items(await ReadAsync($"{Base}/accounts/{account}/chats?top={Math.Clamp(limit, 1, 50)}", ct)).Select(c => new
            {
                chatId = Str(c, "id"),
                topic = Str(c, "topic"),
                chatType = Str(c, "chatType"),
                members = Prop(c, "members") is { ValueKind: JsonValueKind.Array } m ? m.EnumerateArray().Select(x => Str(x, "displayName")).OfType<string>().Take(20).ToList() : null,
                lastMessage = Prop(c, "lastMessagePreview") is { } p ? new { from = Prop(p, "from") is { } f && Prop(f, "user") is { } u ? Str(u, "displayName") : null, at = Str(p, "createdDateTime"), text = Cap(BodyText(Prop(p, "body")), 300) } : null,
                updated = Str(c, "lastUpdatedDateTime"),
            });
            return Serialize(new { accountId = account, chats });
        }

        [McpServerTool(Name = "read_chat_messages", Title = "Read Teams chat", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
        [Description("Reads the latest messages of a Teams chat as plain text, newest first.")]
        [McpAccess(McpArea.Teams, McpAccessLevel.Read)]
        public async Task<string> ReadChatMessages([Description("chatId from list_chats.")] string chatId, Guid? accountId = null, [Description("Messages to return, 1-100.")] int limit = 30, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            var messages = Items(await ReadAsync($"{Base}/accounts/{account}/chats/{Uri.EscapeDataString(chatId)}/messages?top={Math.Clamp(limit, 1, 100)}", ct))
                .Where(m => Str(m, "messageType") is null or "message")
                .Select(m => new { id = Str(m, "id"), from = Prop(m, "from") is { } f && Prop(f, "user") is { } u ? Str(u, "displayName") : null, at = Str(m, "createdDateTime"), text = Cap(BodyText(Prop(m, "body"))) });
            return Serialize(new { accountId = account, chatId, messages });
        }

        [McpServerTool(Name = "send_chat_message", Title = "Send Teams message", ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = true)]
        [Description("Posts a plain-text message (up to 4000 characters) to a Teams chat as the user. Others see it immediately, so confirm with the user first.")]
        [McpAccess(McpArea.Teams, McpAccessLevel.ReadWrite)]
        public async Task<string> SendChatMessage(string chatId, [Description("Message text.")] string text, Guid? accountId = null, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            var result = await WriteAsync(HttpMethods.Post, $"{Base}/accounts/{account}/chats/{Uri.EscapeDataString(chatId)}/messages", new { text }, ct);
            return Serialize(new { sent = true, chatId, messageId = result is { } r ? Str(r, "id") : null });
        }

        [McpServerTool(Name = "list_drive_items", Title = "List OneDrive files", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
        [Description("Lists the files and folders in a OneDrive folder with name, size, type and modification date. path is root, or root:/Folder/Sub: for a subfolder. name_contains filters the folder's entries by name.")]
        [McpAccess(McpArea.Drive, McpAccessLevel.Read)]
        public async Task<string> ListDriveItems([Description("root, or root:/Folder/Sub: (names as shown, slash separated).")] string path = "root", [Description("Only entries whose name contains this text.")] string? nameContains = null, Guid? accountId = null, int offset = 0, int limit = 100, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            var items = (await DriveFolderAsync(account, path, ct))
                .Where(i => nameContains is null || Str(i, "name")?.Contains(nameContains, StringComparison.OrdinalIgnoreCase) == true)
                .Select(i => new { itemId = Str(i, "id"), name = Str(i, "name"), isFolder = Prop(i, "folder") is not null, childCount = Prop(i, "folder") is { } f ? Long(f, "childCount") : null, sizeBytes = Long(i, "size"), mimeType = Prop(i, "file") is { } file ? Str(file, "mimeType") : null, modified = Str(i, "lastModifiedDateTime"), webUrl = Str(i, "webUrl") });
            return Serialize(new { accountId = account, path, items = Page(items, offset, limit, "entries") });
        }

        [McpServerTool(Name = "read_drive_file", Title = "Read OneDrive file", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
        [Description("Returns a small OneDrive file (up to 1 MB): text files as text (paged), others as base64 up to 256 KB. Give the folder path and the itemId from list_drive_items.")]
        [McpAccess(McpArea.Drive, McpAccessLevel.Read)]
        public async Task<string> ReadDriveFile([Description("Folder the file is in, as for list_drive_items.")] string path, [Description("itemId from list_drive_items.")] string itemId, Guid? accountId = null, int offset = 0, int maxChars = DefaultMaxChars, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            var item = (await DriveFolderAsync(account, path, ct)).FirstOrDefault(i => Str(i, "id") == itemId);
            if (item.ValueKind != JsonValueKind.Object)
                throw new McpException("No file with that itemId in that folder. List the folder with list_drive_items first.");
            if (Prop(item, "folder") is not null)
                throw new McpException("That item is a folder. Use list_drive_items with its path.");

            var name = Str(item, "name");
            var size = Long(item, "size") ?? 0;
            if (size > MaxDriveFileBytes)
                return Serialize(new { fileName = name, sizeBytes = size, note = "The file is larger than 1 MB and is not returned here. Open it in OneDrive instead.", webUrl = Str(item, "webUrl") });
            if (Str(item, "@microsoft.graph.downloadUrl") is not { } url || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !DownloadHosts.Any(h => uri.Host.EndsWith(h, StringComparison.OrdinalIgnoreCase)))
                throw new McpException("Microsoft did not offer a download for this file.");

            using var client = httpClientFactory.CreateClient("mcp-drive");
            client.Timeout = TimeSpan.FromSeconds(30);
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                throw new McpException("OneDrive did not return the file. Try again later.");
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length > MaxDriveFileBytes)
                throw new McpException("The file is larger than 1 MB and is not returned here.");
            var mime = Prop(item, "file") is { } file ? Str(file, "mimeType") : response.Content.Headers.ContentType?.MediaType;
            return Serialize(FileContent(bytes, mime, name, MaxBase64Bytes, offset, maxChars));
        }

        [McpServerTool(Name = "list_notebooks", Title = "List OneNote notebooks", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
        [Description("Lists the user's OneNote notebooks with name, last change and a link to open them.")]
        [McpAccess(McpArea.OneNote, McpAccessLevel.Read)]
        public async Task<string> ListNotebooks(Guid? accountId = null, CancellationToken ct = default)
        {
            var account = await AccountAsync(accountId, ct);
            var notebooks = Items(await ReadAsync($"{Base}/accounts/{account}/notebooks", ct)).Select(n => new
            {
                notebookId = Str(n, "id"),
                name = Str(n, "displayName"),
                modified = Str(n, "lastModifiedDateTime"),
                isShared = Bool(n, "isShared"),
                webUrl = Prop(n, "links") is { } l && Prop(l, "oneNoteWebUrl") is { } w ? Str(w, "href") : null,
            });
            return Serialize(new { accountId = account, notebooks });
        }

        [McpServerTool(Name = "list_calendar_events", Title = "List Microsoft 365 calendar", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = true)]
        [Description("Lists Microsoft 365 calendar events of the linked accounts between two dates (at most 62 days), with time, location, organizer and Teams join link.")]
        [McpAccess(McpArea.Calendar, McpAccessLevel.Read)]
        public async Task<string> ListCalendarEvents([Description("Start, ISO 8601 date or date-time. Defaults to now.")] DateTimeOffset? from = null, [Description("End, ISO 8601. Defaults to 7 days after from.")] DateTimeOffset? to = null, CancellationToken ct = default)
        {
            var caller = Caller(accessor);
            var start = from ?? DateTimeOffset.UtcNow;
            var end = to ?? start.AddDays(7);
            var response = await plugins.GetAsync($"{Base}/calendar?start={Uri.EscapeDataString(start.ToString("o"))}&end={Uri.EscapeDataString(end.ToString("o"))}", ct);
            var data = Ensure(response) ?? default;
            var events = Items(Prop(data, "events"))
                .Where(e => Guid.TryParse(Str(e, "accountId"), out var id) && caller.Permissions.AllowsMicrosoftAccount(id))
                .Select(e => new { title = Str(e, "title"), start = Str(e, "start"), end = Str(e, "end"), isAllDay = Bool(e, "isAllDay"), location = Str(e, "location"), organizer = Str(e, "organizer"), preview = Str(e, "preview") is { } p ? Cap(p, 300) : null, joinUrl = Str(e, "joinUrl"), isCancelled = Bool(e, "isCancelled"), accountId = Str(e, "accountId") });
            return Serialize(new { from = start, to = end, events });
        }

        private static object MailSummary(JsonElement m) => new
        {
            id = Str(m, "id"),
            subject = Str(m, "subject"),
            from = Address(Prop(m, "from")),
            received = Str(m, "receivedDateTime"),
            isRead = Bool(m, "isRead"),
            hasAttachments = Bool(m, "hasAttachments"),
            flag = Prop(m, "flag") is { } f ? Str(f, "flagStatus") : null,
            preview = Str(m, "bodyPreview") is { } p ? Cap(p, 300) : null,
        };

        private async Task<List<JsonElement>> DriveFolderAsync(Guid account, string path, CancellationToken ct) =>
            Items(await ReadAsync($"{Base}/accounts/{account}/files?path={Uri.EscapeDataString(path)}&top=500", ct)).ToList();

        /// <summary>The account to use: the one asked for if this connection may use it, otherwise the first allowed one.</summary>
        private async Task<Guid> AccountAsync(Guid? requested, CancellationToken ct)
        {
            var caller = Caller(accessor);
            var accounts = Unwrap(await mediator.Send(new GetMyLinkedAccountsQuery(LinkedProviders.Microsoft), ct))
                .Where(a => caller.Permissions.AllowsMicrosoftAccount(a.Id))
                .ToList();
            if (requested is { } id)
                return accounts.Any(a => a.Id == id) ? id : throw new McpException("That Microsoft account is not linked, or this connection is not allowed to use it.");
            return accounts.FirstOrDefault()?.Id ?? throw new McpException("No Microsoft 365 account is linked to Schuly (or none is allowed for this connection). Link one in the Schuly app first.");
        }

        private async Task<JsonElement?> ReadAsync(string path, CancellationToken ct) => Ensure(await plugins.GetAsync(path, ct));

        private async Task<JsonElement?> WriteAsync(string method, string path, object? body, CancellationToken ct) => Ensure(await plugins.SendAsync(method, path, body, ct));

        private static JsonElement? Ensure(PluginApiResponse response)
        {
            if (response.IsSuccess)
                return response.Json();
            var message = response.Json() is { } error ? Str(error, "message") : null;
            throw new McpException(response.StatusCode switch
            {
                StatusCodes.Status404NotFound => "Not found. Either the item does not exist or the Microsoft 365 plugin is not installed on this Schuly server.",
                StatusCodes.Status409Conflict => "The Microsoft sign-in for this account expired. Reconnect the account in the Schuly app.",
                StatusCodes.Status400BadRequest => message ?? "Microsoft 365 rejected the request. Check the ids and parameters.",
                StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden => "Schuly refused this request for the signed-in user.",
                _ => "Microsoft 365 did not answer. Try again later.",
            });
        }
    }
}
