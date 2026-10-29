using System.Net;
using System.Text.Json;
using Schuly.API.Linking;
using Schuly.Infrastructure.Plugins;
using Schuly.Plugin.Abstractions;
using Schuly.Plugin.Abstractions.Linking;
using Schuly.Tests.TestHelpers;

namespace Schuly.Tests
{
    public class LinkingTests
    {
        private const string Start = "/api/linking/providers/test-provider/start";

        private static object WebStart => new { platform = "web" };

        private static async Task<string> StartSession(LinkingTestHost host, Guid? user = null)
        {
            var (response, json) = await host.Json(HttpMethod.Post, Start, user ?? host.UserA, WebStart);
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            return json.GetProperty("sessionId").GetString()!;
        }

        private static object Credentials(string password = "pw") => new { stepId = "credentials", values = new Dictionary<string, string> { ["email"] = "a@b.ch", ["password"] = password } };

        [Test]
        public async Task Start_submit_done_happy_path_removes_the_session()
        {
            var provider = new ScriptedLinkProvider();
            await using var host = await LinkingTestHost.StartAsync(provider);

            var (_, start) = await host.Json(HttpMethod.Post, Start, host.UserA, WebStart);
            await Assert.That(start.GetProperty("status").GetString()).IsEqualTo("active");
            await Assert.That(start.GetProperty("step").GetProperty("kind").GetString()).IsEqualTo("form");
            await Assert.That(start.GetProperty("providerId").GetString()).IsEqualTo("test-provider");
            var id = start.GetProperty("sessionId").GetString()!;

            var (response, done) = await host.Json(HttpMethod.Post, $"/api/linking/sessions/{id}/submit", host.UserA, Credentials());
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(done.GetProperty("status").GetString()).IsEqualTo("done");
            await Assert.That(done.GetProperty("step").GetProperty("done").GetProperty("accountId").GetString()).IsEqualTo("acc-1");
            await Assert.That(provider.Submits.Single().State).IsEqualTo("start-state");
            await Assert.That(provider.Submits.Single().HostCallbackUrl).IsEqualTo($"http://localhost/api/linking/callback?session={id}");
            await Assert.That(host.Sessions.Count).IsEqualTo(0);
            await Assert.That((await host.Send(HttpMethod.Get, $"/api/linking/sessions/{id}", host.UserA)).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }

        [Test]
        public async Task Plugin_state_is_encrypted_in_the_store_and_never_sent_to_the_client()
        {
            await using var host = await LinkingTestHost.StartAsync(new ScriptedLinkProvider { OnStart = _ => new LinkStepResult(ScriptedLinkProvider.CredentialsStep, "plugin-secret-state") });
            var (response, _) = await host.Json(HttpMethod.Post, Start, host.UserA, WebStart);
            var body = await (await host.Send(HttpMethod.Post, Start, host.UserA, WebStart)).Content.ReadAsStringAsync();

            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(body.Contains("plugin-secret-state")).IsFalse();
            foreach (var session in host.Sessions.Snapshot())
                await Assert.That(session.ProtectedState!.Contains("plugin-secret-state")).IsFalse();
        }

        [Test]
        public async Task Submitting_a_stale_step_id_is_a_bad_request()
        {
            await using var host = await LinkingTestHost.StartAsync(new ScriptedLinkProvider());
            var id = await StartSession(host);
            var (response, json) = await host.Json(HttpMethod.Post, $"/api/linking/sessions/{id}/submit", host.UserA, new { stepId = "other", values = new Dictionary<string, string>() });
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            await Assert.That(json.GetProperty("message").GetString()).IsNotNull();
        }

        [Test]
        public async Task Validation_errors_name_field_keys_but_never_values()
        {
            var provider = new ScriptedLinkProvider();
            await using var host = await LinkingTestHost.StartAsync(provider);
            var id = await StartSession(host);

            var (response, json) = await host.Json(HttpMethod.Post, $"/api/linking/sessions/{id}/submit", host.UserA, new { stepId = "credentials", values = new Dictionary<string, string> { ["email"] = "not-an-email-VALUE", ["password"] = "x", ["extra"] = "y" } });
            var text = json.ToString();

            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            await Assert.That(text.Contains("email")).IsTrue();
            await Assert.That(text.Contains("not-an-email-VALUE")).IsFalse();
            await Assert.That(provider.Submits.Count).IsEqualTo(0);
        }

        [Test]
        public async Task Another_users_session_is_not_found()
        {
            var provider = new ScriptedLinkProvider();
            await using var host = await LinkingTestHost.StartAsync(provider);
            var id = await StartSession(host);

            await Assert.That((await host.Send(HttpMethod.Get, $"/api/linking/sessions/{id}", host.UserB)).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That((await host.Send(HttpMethod.Post, $"/api/linking/sessions/{id}/submit", host.UserB, Credentials())).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            await host.Send(HttpMethod.Delete, $"/api/linking/sessions/{id}", host.UserB);
            await Assert.That(host.Sessions.Count).IsEqualTo(1);
            await Assert.That(provider.Submits.Count).IsEqualTo(0);
        }

        [Test]
        public async Task Unauthenticated_calls_are_rejected()
        {
            await using var host = await LinkingTestHost.StartAsync(new ScriptedLinkProvider());
            await Assert.That((await host.Send(HttpMethod.Get, "/api/linking/providers", null)).StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
            await Assert.That((await host.Send(HttpMethod.Get, "/api/linking/accounts", null)).StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        }

        [Test]
        public async Task A_session_expires_after_ten_minutes_and_the_sweeper_removes_it()
        {
            await using var host = await LinkingTestHost.StartAsync(new ScriptedLinkProvider());
            var id = await StartSession(host);

            host.Time.Advance(TimeSpan.FromMinutes(9));
            await Assert.That((await host.Send(HttpMethod.Get, $"/api/linking/sessions/{id}", host.UserA)).StatusCode).IsEqualTo(HttpStatusCode.OK);

            host.Time.Advance(TimeSpan.FromMinutes(2));
            var (_, json) = await host.Json(HttpMethod.Get, $"/api/linking/sessions/{id}", host.UserA);
            await Assert.That(json.GetProperty("status").GetString()).IsEqualTo("expired");

            var other = await StartSession(host);
            host.Time.Advance(TimeSpan.FromMinutes(11));
            await Assert.That(host.Sessions.Sweep()).IsEqualTo(1);
            await Assert.That((await host.Send(HttpMethod.Get, $"/api/linking/sessions/{other}", host.UserA)).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }

        [Test]
        public async Task Start_is_rate_limited_per_user_with_a_json_message()
        {
            await using var host = await LinkingTestHost.StartAsync(new ScriptedLinkProvider());
            for (var i = 0; i < 10; i++)
                await Assert.That((await host.Send(HttpMethod.Post, Start, host.UserA, WebStart)).StatusCode).IsEqualTo(HttpStatusCode.OK);

            var (response, json) = await host.Json(HttpMethod.Post, Start, host.UserA, WebStart);
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.TooManyRequests);
            await Assert.That(json.GetProperty("message").GetString()).IsNotNull();
            await Assert.That((await host.Send(HttpMethod.Post, Start, host.UserB, WebStart)).StatusCode).IsEqualTo(HttpStatusCode.OK);
        }

        [Test]
        public async Task Submit_is_rate_limited_at_twenty_a_minute()
        {
            await using var host = await LinkingTestHost.StartAsync(new ScriptedLinkProvider());
            var id = await StartSession(host);
            HttpStatusCode last = HttpStatusCode.OK;
            var okCount = 0;
            for (var i = 0; i < 21; i++)
            {
                last = (await host.Send(HttpMethod.Post, $"/api/linking/sessions/{id}/submit", host.UserA, new { stepId = "wrong", values = new Dictionary<string, string>() })).StatusCode;
                if (last == HttpStatusCode.BadRequest)
                    okCount++;
            }
            await Assert.That(okCount).IsEqualTo(20);
            await Assert.That(last).IsEqualTo(HttpStatusCode.TooManyRequests);
        }

        [Test]
        public async Task Polling_is_rate_limited_at_sixty_a_minute()
        {
            await using var host = await LinkingTestHost.StartAsync(new ScriptedLinkProvider());
            var id = await StartSession(host);
            HttpStatusCode last = HttpStatusCode.OK;
            for (var i = 0; i < 61; i++)
                last = (await host.Send(HttpMethod.Get, $"/api/linking/sessions/{id}", host.UserA)).StatusCode;
            await Assert.That(last).IsEqualTo(HttpStatusCode.TooManyRequests);
        }

        [Test]
        public async Task Start_is_also_limited_per_client_ip_from_the_resolved_remote_address_only()
        {
            await using var host = await LinkingTestHost.StartAsync(new ScriptedLinkProvider());
            var statuses = new List<HttpStatusCode>();
            for (var i = 0; i < 31; i++)
            {
                // A different user and a different spoofed X-Forwarded-For each time: only the resolved remote address counts.
                var request = host.Request(HttpMethod.Post, Start, Guid.NewGuid(), WebStart, ip: "203.0.113.7");
                request.Headers.Add("X-Forwarded-For", $"198.51.100.{i + 1}");
                statuses.Add((await host.Client.SendAsync(request)).StatusCode);
            }

            await Assert.That(statuses.Take(30).All(s => s == HttpStatusCode.OK)).IsTrue();
            await Assert.That(statuses[30]).IsEqualTo(HttpStatusCode.TooManyRequests);
            await Assert.That((await host.Send(HttpMethod.Post, Start, Guid.NewGuid(), WebStart, ip: "203.0.113.8")).StatusCode).IsEqualTo(HttpStatusCode.OK);
        }

        [Test]
        public async Task Submit_is_limited_per_client_ip_at_sixty_a_minute()
        {
            await using var host = await LinkingTestHost.StartAsync(new ScriptedLinkProvider());
            HttpStatusCode last = HttpStatusCode.OK;
            for (var i = 0; i < 61; i++)
                last = (await host.Send(HttpMethod.Post, "/api/linking/sessions/nope/submit", Guid.NewGuid(), new { stepId = "x" }, ip: "203.0.113.9")).StatusCode;
            await Assert.That(last).IsEqualTo(HttpStatusCode.TooManyRequests);
        }

        [Test]
        public async Task Cancel_removes_the_session_and_later_submits_are_not_found()
        {
            var provider = new ScriptedLinkProvider();
            await using var host = await LinkingTestHost.StartAsync(provider);
            var id = await StartSession(host);

            await Assert.That((await host.Send(HttpMethod.Delete, $"/api/linking/sessions/{id}", host.UserA)).StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            await Assert.That((await host.Send(HttpMethod.Post, $"/api/linking/sessions/{id}/submit", host.UserA, Credentials())).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(host.Sessions.Count).IsEqualTo(0);
        }

        [Test]
        public async Task The_reserved_cancel_action_ends_the_session()
        {
            var provider = new ScriptedLinkProvider();
            await using var host = await LinkingTestHost.StartAsync(provider);
            var id = await StartSession(host);
            var (_, json) = await host.Json(HttpMethod.Post, $"/api/linking/sessions/{id}/submit", host.UserA, new { stepId = "credentials", action = "cancel" });
            await Assert.That(json.GetProperty("status").GetString()).IsEqualTo("failed");
            await Assert.That(provider.Submits.Count).IsEqualTo(0);
            await Assert.That(host.Sessions.Count).IsEqualTo(0);
        }

        [Test]
        public async Task A_throwing_plugin_fails_the_session_with_a_generic_message_and_no_exception_text_is_logged()
        {
            var provider = new ScriptedLinkProvider { OnSubmit = c => throw new InvalidOperationException("boom-" + c.Values["password"]) };
            await using var host = await LinkingTestHost.StartAsync(provider);
            var id = await StartSession(host);

            var (response, json) = await host.Json(HttpMethod.Post, $"/api/linking/sessions/{id}/submit", host.UserA, Credentials("hunter2-SECRET"));
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(json.GetProperty("status").GetString()).IsEqualTo("failed");
            await Assert.That(json.ToString().Contains("boom")).IsFalse();
            await Assert.That(string.Join("\n", host.LogLines).Contains("hunter2")).IsFalse();
            await Assert.That(host.Sessions.Count).IsEqualTo(0);
        }

        [Test]
        public async Task A_submitted_password_never_appears_in_logs_responses_or_the_session_store()
        {
            const string password = "P@ssw0rd-NEVER-LEAK-9f3";
            var provider = new ScriptedLinkProvider
            {
                OnSubmit = c => new LinkStepResult(new LinkStep { Id = "credentials", Kind = LinkStepKind.Form, Error = "Wrong email or password.", Fields = ScriptedLinkProvider.CredentialsStep.Fields }, "state"),
            };
            await using var host = await LinkingTestHost.StartAsync(provider);
            var id = await StartSession(host);

            var response = await host.Send(HttpMethod.Post, $"/api/linking/sessions/{id}/submit", host.UserA, Credentials(password));
            var body = await response.Content.ReadAsStringAsync();
            var invalid = await (await host.Send(HttpMethod.Post, $"/api/linking/sessions/{id}/submit", host.UserA, new { stepId = "credentials", values = new Dictionary<string, string> { ["email"] = password, ["password"] = password } })).Content.ReadAsStringAsync();
            var again = await (await host.Send(HttpMethod.Get, $"/api/linking/sessions/{id}", host.UserA)).Content.ReadAsStringAsync();

            await Assert.That(body.Contains("Wrong email or password.")).IsTrue();
            await Assert.That(body.Contains(password)).IsFalse();
            await Assert.That(invalid.Contains(password)).IsFalse();
            await Assert.That(again.Contains(password)).IsFalse();
            await Assert.That(string.Join("\n", host.LogLines).Contains(password)).IsFalse();
            foreach (var session in host.Sessions.Snapshot())
            {
                var stored = JsonSerializer.Serialize(session.Step) + session.ProtectedState + session.HostCallbackUrl + session.ReauthAccountId;
                await Assert.That(stored.Contains(password)).IsFalse();
            }
        }

        public static IEnumerable<Func<LinkStep>> InvalidSteps()
        {
            yield return () => new LinkStep { Id = "device", Kind = LinkStepKind.DeviceCode, DeviceCode = new LinkDeviceCode("ABCD", "http://microsoft.example/devicelogin", DateTimeOffset.UtcNow.AddMinutes(5), 5) };
            yield return () => new LinkStep { Id = "browser", Kind = LinkStepKind.Redirect, Redirect = new LinkRedirect("https://login.example/authorize", LinkRedirectMode.Capture, "https://cb.example/", []) };
            yield return () => new LinkStep { Id = "redir", Kind = LinkStepKind.Redirect, Redirect = new LinkRedirect("http://login.example/authorize", LinkRedirectMode.HostCallback, null, []) };
            yield return () => new LinkStep { Id = "form", Kind = LinkStepKind.Form, Fields = [new LinkField { Key = "pw", Label = "Pw", Type = LinkFieldType.Password, Secret = true, DefaultValue = "preset" }] };
            yield return () => new LinkStep { Id = "bad key", Kind = LinkStepKind.Form, Fields = [new LinkField { Key = "1bad", Label = "x" }] };
        }

        [Test]
        [MethodDataSource(nameof(InvalidSteps))]
        public async Task An_invalid_step_from_the_plugin_fails_the_session(LinkStep invalid)
        {
            var provider = new ScriptedLinkProvider { OnStart = _ => new LinkStepResult(invalid, "s") };
            await using var host = await LinkingTestHost.StartAsync(provider);
            var (response, json) = await host.Json(HttpMethod.Post, Start, host.UserA, WebStart);

            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(json.GetProperty("status").GetString()).IsEqualTo("failed");
            await Assert.That(json.GetProperty("step").GetProperty("kind").GetString()).IsEqualTo("error");
            await Assert.That(json.GetProperty("step").GetProperty("error").GetString()).IsEqualTo(LinkingService.GenericFailure);
            await Assert.That(host.Sessions.Count).IsEqualTo(0);
        }

        [Test]
        public async Task A_capture_redirect_is_accepted_on_mobile_but_rejected_on_web()
        {
            var capture = new LinkStep { Id = "browser", Kind = LinkStepKind.Redirect, Redirect = new LinkRedirect("https://login.example/authorize", LinkRedirectMode.Capture, "https://cb.example/", ["login.example"]) };
            var provider = new ScriptedLinkProvider { OnStart = _ => new LinkStepResult(capture, null) };
            await using var host = await LinkingTestHost.StartAsync(provider);

            var (_, mobile) = await host.Json(HttpMethod.Post, Start, host.UserA, new { platform = "mobile" });
            var (_, web) = await host.Json(HttpMethod.Post, Start, host.UserA, new { platform = "web" });

            await Assert.That(mobile.GetProperty("status").GetString()).IsEqualTo("active");
            await Assert.That(mobile.GetProperty("step").GetProperty("redirect").GetProperty("mode").GetString()).IsEqualTo("capture");
            await Assert.That(web.GetProperty("status").GetString()).IsEqualTo("failed");
        }

        [Test]
        public async Task Platform_comes_from_the_body_else_from_the_web_client_header()
        {
            var provider = new ScriptedLinkProvider
            {
                Descriptor = new LinkProviderDescriptor
                {
                    Id = "test-provider",
                    DisplayName = "Test",
                    Flows = [new LinkFlowDescriptor { Id = "web-only", Label = "Web", Platforms = LinkPlatforms.Web }, new LinkFlowDescriptor { Id = "app-only", Label = "App", Platforms = LinkPlatforms.Mobile }],
                },
            };
            await using var host = await LinkingTestHost.StartAsync(provider);

            var (_, byHeader) = await host.Json(HttpMethod.Post, Start, host.UserA, new { }, client: "web");
            var (_, byBody) = await host.Json(HttpMethod.Post, Start, host.UserA, new { platform = "mobile" }, client: "web");
            var (_, none) = await host.Json(HttpMethod.Post, Start, host.UserA, new { });
            var (badFlow, _) = await host.Json(HttpMethod.Post, Start, host.UserA, new { platform = "web", flowId = "app-only" });

            await Assert.That(byHeader.GetProperty("flowId").GetString()).IsEqualTo("web-only");
            await Assert.That(byBody.GetProperty("flowId").GetString()).IsEqualTo("app-only");
            await Assert.That(none.GetProperty("flowId").GetString()).IsEqualTo("app-only");
            await Assert.That(badFlow.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        }

        [Test]
        public async Task Providers_are_listed_with_flows_filtered_by_platform()
        {
            var multi = new ScriptedLinkProvider("multi")
            {
                Descriptor = new LinkProviderDescriptor
                {
                    Id = "multi",
                    DisplayName = "Multi",
                    Icon = new LinkIcon { Lucide = "mail" },
                    Flows = [new LinkFlowDescriptor { Id = "web-only", Label = "Web", Platforms = LinkPlatforms.Web }],
                },
            };
            await using var host = await LinkingTestHost.StartAsync(new ScriptedLinkProvider(), multi);

            var (_, web) = await host.Json(HttpMethod.Get, "/api/linking/providers?platform=web", host.UserA);
            var (_, mobile) = await host.Json(HttpMethod.Get, "/api/linking/providers?platform=mobile", host.UserA);

            await Assert.That(web.GetArrayLength()).IsEqualTo(2);
            await Assert.That(mobile.GetArrayLength()).IsEqualTo(1);
            await Assert.That(web.EnumerateArray().First(p => p.GetProperty("id").GetString() == "multi").GetProperty("icon").GetProperty("lucide").GetString()).IsEqualTo("mail");
        }

        [Test]
        public async Task Secret_default_values_are_stripped_from_the_step_dto()
        {
            var step = new LinkStep { Id = "f", Kind = LinkStepKind.Form, Fields = [new LinkField { Key = "pw", Label = "Pw", Type = LinkFieldType.Password, Secret = true, DefaultValue = "leak" }, new LinkField { Key = "host", Label = "Host", DefaultValue = "https://x.example" }] };
            var dto = LinkDtoMapper.ToDto(step);
            await Assert.That(dto.Fields[0].DefaultValue).IsNull();
            await Assert.That(dto.Fields[0].Secret).IsTrue();
            await Assert.That(dto.Fields[1].DefaultValue).IsEqualTo("https://x.example");
        }

        [Test]
        public async Task Push_code_polling_is_throttled_to_the_step_interval()
        {
            var push = new LinkStep { Id = "push", Kind = LinkStepKind.PushCode, PushCode = new LinkPushCode("42", DateTimeOffset.UtcNow.AddMinutes(2), 5) };
            var provider = new ScriptedLinkProvider
            {
                OnStart = _ => new LinkStepResult(push, "ps"),
                OnPoll = c => c.State == "ps" ? LinkStepResult.Done("acc-9", "Done") : LinkStepResult.Fail("state lost"),
            };
            await using var host = await LinkingTestHost.StartAsync(provider);
            var id = await StartSession(host);

            await host.Send(HttpMethod.Get, $"/api/linking/sessions/{id}", host.UserA);
            await Assert.That(provider.PollCalls).IsEqualTo(0);

            host.Time.Advance(TimeSpan.FromSeconds(6));
            var (_, json) = await host.Json(HttpMethod.Get, $"/api/linking/sessions/{id}", host.UserA);
            await Assert.That(provider.PollCalls).IsEqualTo(1);
            await Assert.That(json.GetProperty("status").GetString()).IsEqualTo("done");
        }

        [Test]
        public async Task Polling_again_inside_the_interval_returns_the_cached_step()
        {
            var push = new LinkStep { Id = "push", Kind = LinkStepKind.PushCode, PushCode = new LinkPushCode("42", DateTimeOffset.UtcNow.AddMinutes(2), 5) };
            var provider = new ScriptedLinkProvider { OnStart = _ => new LinkStepResult(push, "ps"), OnPoll = _ => new LinkStepResult(push, "ps") };
            await using var host = await LinkingTestHost.StartAsync(provider);
            var id = await StartSession(host);

            host.Time.Advance(TimeSpan.FromSeconds(6));
            for (var i = 0; i < 4; i++)
                await host.Send(HttpMethod.Get, $"/api/linking/sessions/{id}", host.UserA);
            await Assert.That(provider.PollCalls).IsEqualTo(1);

            host.Time.Advance(TimeSpan.FromSeconds(6));
            await host.Send(HttpMethod.Get, $"/api/linking/sessions/{id}", host.UserA);
            await Assert.That(provider.PollCalls).IsEqualTo(2);
        }

        [Test]
        public async Task A_push_session_does_not_outlive_its_step_by_more_than_the_grace()
        {
            var push = new LinkStep { Id = "push", Kind = LinkStepKind.PushCode, PushCode = new LinkPushCode("42", DateTimeOffset.UtcNow.AddMinutes(1), 5) };
            await using var host = await LinkingTestHost.StartAsync(new ScriptedLinkProvider { OnStart = _ => new LinkStepResult(push, null) });
            var id = await StartSession(host);

            host.Time.Advance(TimeSpan.FromMinutes(2));
            var (_, json) = await host.Json(HttpMethod.Get, $"/api/linking/sessions/{id}", host.UserA);
            await Assert.That(json.GetProperty("status").GetString()).IsEqualTo("expired");
        }

        [Test]
        public async Task Concurrent_submits_on_one_session_are_serialized()
        {
            var running = 0;
            var maxRunning = 0;
            var provider = new ScriptedLinkProvider
            {
                OnSubmit = _ =>
                {
                    var now = Interlocked.Increment(ref running);
                    lock (provider0) { maxRunning = Math.Max(maxRunning, now); }
                    Thread.Sleep(80);
                    Interlocked.Decrement(ref running);
                    return new LinkStepResult(ScriptedLinkProvider.CredentialsStep, "s");
                },
            };
            await using var host = await LinkingTestHost.StartAsync(provider);
            var id = await StartSession(host);

            var calls = Enumerable.Range(0, 4).Select(_ => host.Send(HttpMethod.Post, $"/api/linking/sessions/{id}/submit", host.UserA, Credentials())).ToList();
            await Task.WhenAll(calls);

            await Assert.That(maxRunning).IsEqualTo(1);
            await Assert.That(provider.Submits.Count).IsEqualTo(4);
        }

        private static readonly object provider0 = new();

        [Test]
        public async Task The_callback_route_resumes_the_session_anonymously_and_redirects()
        {
            var redirect = new LinkStep { Id = "browser", Kind = LinkStepKind.Redirect, Redirect = new LinkRedirect("https://login.example/authorize", LinkRedirectMode.HostCallback, null, []) };
            var provider = new ScriptedLinkProvider
            {
                OnStart = _ => new LinkStepResult(redirect, "cb-state"),
                OnSubmit = c => c.Action == "callback" && c.Values.GetValueOrDefault("code") == "abc" && c.State == "cb-state" && c.StepId == "browser" ? LinkStepResult.Done("acc-cb", "Via callback") : LinkStepResult.Fail("unexpected"),
            };
            await using var host = await LinkingTestHost.StartAsync(provider);
            var id = await StartSession(host);

            var callback = await host.Send(HttpMethod.Get, $"/api/linking/callback?session={id}&code=abc&state=zzz", null);
            await Assert.That(callback.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            await Assert.That(callback.Headers.Location!.ToString()).IsEqualTo($"/settings?linkSession={id}");

            var (_, json) = await host.Json(HttpMethod.Get, $"/api/linking/sessions/{id}", host.UserA);
            await Assert.That(json.GetProperty("status").GetString()).IsEqualTo("done");
            await Assert.That(json.GetProperty("step").GetProperty("done").GetProperty("accountId").GetString()).IsEqualTo("acc-cb");
            await Assert.That((await host.Send(HttpMethod.Get, $"/api/linking/sessions/{id}", host.UserA)).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }

        [Test]
        public async Task The_callback_with_an_unknown_session_redirects_without_calling_a_plugin()
        {
            var provider = new ScriptedLinkProvider();
            await using var host = await LinkingTestHost.StartAsync(provider);
            var callback = await host.Send(HttpMethod.Get, "/api/linking/callback?session=nope&code=abc", null);
            await Assert.That(callback.StatusCode).IsEqualTo(HttpStatusCode.Redirect);
            await Assert.That(callback.Headers.Location!.ToString()).IsEqualTo("/settings");
            await Assert.That(provider.Submits.Count).IsEqualTo(0);
        }

        [Test]
        public async Task Accounts_fan_out_and_one_failing_provider_does_not_break_the_list()
        {
            var good = new ScriptedLinkProvider("good") { OnList = _ => [new LinkedAccountSummary("a1", "Alex", "alex@school.ch", LinkedAccountStatus.Syncing, DateTimeOffset.UtcNow, null, null)] };
            var bad = new ScriptedLinkProvider("bad") { OnList = _ => throw new InvalidOperationException("secret detail") };
            await using var host = await LinkingTestHost.StartAsync(good, bad);

            var (response, json) = await host.Json(HttpMethod.Get, "/api/linking/accounts", host.UserA);
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            var items = json.EnumerateArray().ToList();
            await Assert.That(items.Count).IsEqualTo(2);

            var ok = items.First(i => i.GetProperty("providerId").GetString() == "good");
            await Assert.That(ok.GetProperty("status").GetString()).IsEqualTo("syncing");
            await Assert.That(ok.GetProperty("capabilities").GetArrayLength()).IsEqualTo(1);
            await Assert.That(ok.GetProperty("canReauth").GetBoolean()).IsTrue();

            var failed = items.First(i => i.GetProperty("providerId").GetString() == "bad");
            await Assert.That(failed.GetProperty("status").GetString()).IsEqualTo("error");
            await Assert.That(failed.ToString().Contains("secret detail")).IsFalse();
        }

        [Test]
        public async Task Unlink_returns_204_or_404()
        {
            var provider = new ScriptedLinkProvider { OnUnlink = (_, id) => id == "acc-1" };
            await using var host = await LinkingTestHost.StartAsync(provider);

            await Assert.That((await host.Send(HttpMethod.Delete, "/api/linking/accounts/test-provider/acc-1", host.UserA)).StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            await Assert.That((await host.Send(HttpMethod.Delete, "/api/linking/accounts/test-provider/nope", host.UserA)).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That((await host.Send(HttpMethod.Delete, "/api/linking/accounts/unknown/acc-1", host.UserA)).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }

        [Test]
        public async Task Icons_are_served_sanitized_with_a_strict_csp_and_anonymously()
        {
            const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\" onload=\"alert(1)\"><script>alert(1)</script><path d=\"M0 0h24v24H0z\" fill=\"red\"/></svg>";
            var provider = new ScriptedLinkProvider { Descriptor = new LinkProviderDescriptor { Id = "test-provider", DisplayName = "T", Icon = new LinkIcon { Svg = svg }, Flows = [new LinkFlowDescriptor { Id = "f1", Label = "F" }] } };
            await using var host = await LinkingTestHost.StartAsync(provider);

            var (_, list) = await host.Json(HttpMethod.Get, "/api/linking/providers?platform=web", host.UserA);
            var url = list[0].GetProperty("icon").GetProperty("svgUrl").GetString()!;
            var response = await host.Send(HttpMethod.Get, url, null);
            var body = await response.Content.ReadAsStringAsync();

            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(response.Content.Headers.ContentType!.MediaType).IsEqualTo("image/svg+xml");
            await Assert.That(response.Headers.GetValues("Content-Security-Policy").Single()).IsEqualTo("default-src 'none'; style-src 'unsafe-inline'");
            await Assert.That(response.Headers.GetValues("X-Content-Type-Options").Single()).IsEqualTo("nosniff");
            await Assert.That(response.Headers.CacheControl!.ToString()).Contains("max-age");
            await Assert.That(body.Contains("script")).IsFalse();
            await Assert.That(body.Contains("onload")).IsFalse();
            await Assert.That(body.Contains("<path")).IsTrue();
            await Assert.That((await host.Send(HttpMethod.Get, "/api/linking/providers/none/icon.svg", null)).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }
    }
}
