import type { BrandName } from '../shared/brand-icon';

export interface McpSnippet {
  title: string;
  code: string;
}

export interface McpClientGuide {
  id: string;
  name: string;
  icon: string;
  /** A monochrome client logo; the Lucide icon is the fallback. */
  brand?: BrandName;
  steps: string[];
  snippet: McpSnippet;
  note?: string;
  key: { intro: string; snippet: McpSnippet | null };
  installLink?: { label: string; href: string };
}

export const MCP_KEY_PLACEHOLDER = 'schuly_mcp_YOUR_KEY';

export function mcpUrl(origin: string): string {
  return `${origin}/mcp`;
}

export function cursorInstallLink(url: string): string {
  const config = btoa(JSON.stringify({ url }));
  return `cursor://anysphere.cursor-deeplink/mcp/install?name=schuly&config=${encodeURIComponent(config)}`;
}

export function vscodeInstallLink(url: string): string {
  return `vscode:mcp/install?${encodeURIComponent(JSON.stringify({ name: 'schuly', type: 'http', url }))}`;
}

const json = (value: unknown) => JSON.stringify(value, null, 2);

export function mcpClientGuides(url: string): McpClientGuide[] {
  const bearerEnv = 'Bearer ${env:SCHULY_MCP_KEY}';
  return [
    {
      id: 'claude-code',
      name: 'Claude Code',
      icon: 'lucideTerminal',
      brand: 'claude',
      steps: [
        'Run this command in your terminal.',
        'Start Claude Code and run /mcp.',
        'Choose Schuly, then Authenticate, and finish the sign-in in your browser.',
      ],
      snippet: { title: 'Terminal', code: `claude mcp add --transport http schuly ${url}` },
      key: {
        intro: 'Add the key as a header when you register the server.',
        snippet: {
          title: 'Terminal',
          code: `claude mcp add --transport http schuly ${url} --header "Authorization: Bearer ${MCP_KEY_PLACEHOLDER}"`,
        },
      },
    },
    {
      id: 'claude-desktop',
      name: 'Claude Desktop',
      icon: 'lucideMonitor',
      brand: 'claude',
      steps: [
        'Open Settings (Customize) and go to Connectors.',
        'Choose Add custom connector.',
        `Enter the name Schuly and the URL ${url}, then choose Add.`,
        'Choose Connect and sign in to Schuly in your browser.',
      ],
      snippet: { title: 'Connector URL', code: url },
      note: "This also applies to claude.ai. The connector is reached from Anthropic's cloud, so Schuly must be available at a public URL.",
      key: {
        intro:
          "If your plan offers custom headers, open the connector's advanced settings and add this request header.",
        snippet: { title: 'Request header', code: `Authorization: Bearer ${MCP_KEY_PLACEHOLDER}` },
      },
    },
    {
      id: 'chatgpt',
      name: 'ChatGPT',
      icon: 'lucideMessageSquare',
      brand: 'chatgpt',
      steps: [
        'Open Settings, then Apps & Connectors, then Advanced settings, and turn on Developer mode.',
        'Choose Create (or open chatgpt.com/plugins, choose +, then Add custom MCP server).',
        `Enter the name Schuly and the URL ${url}.`,
        'Set authentication to OAuth, create it, and sign in to Schuly in your browser.',
      ],
      snippet: { title: 'Server URL', code: url },
      note: 'ChatGPT connects from the cloud, so it needs the public URL of Schuly.',
      key: {
        intro: 'ChatGPT only supports OAuth sign-in, so API keys do not work there.',
        snippet: null,
      },
    },
    {
      id: 'cursor',
      name: 'Cursor',
      icon: 'lucideCode',
      brand: 'cursor',
      steps: [
        'Use the install button, or add this to ~/.cursor/mcp.json.',
        'Open Cursor Settings, then MCP.',
        'Next to Schuly, click Needs login and finish the sign-in in your browser.',
      ],
      snippet: { title: '~/.cursor/mcp.json', code: json({ mcpServers: { schuly: { url } } }) },
      installLink: { label: 'Install in Cursor', href: cursorInstallLink(url) },
      key: {
        intro: 'Add a headers entry and put the key in the SCHULY_MCP_KEY environment variable.',
        snippet: {
          title: '~/.cursor/mcp.json',
          code: json({ mcpServers: { schuly: { url, headers: { Authorization: bearerEnv } } } }),
        },
      },
    },
    {
      id: 'vscode',
      name: 'VS Code',
      icon: 'lucideCode',
      brand: 'vscode',
      steps: [
        'Use the install button, or add this to .vscode/mcp.json.',
        'Start the Schuly server from the file or the MCP view.',
        'Choose Allow when asked to sign in, and finish the sign-in in your browser.',
      ],
      snippet: {
        title: '.vscode/mcp.json',
        code: json({ servers: { schuly: { type: 'http', url } } }),
      },
      note: 'VS Code (GitHub Copilot) uses the key "servers", not "mcpServers".',
      installLink: { label: 'Install in VS Code', href: vscodeInstallLink(url) },
      key: {
        intro: 'VS Code asks for the key once and keeps it out of the file.',
        snippet: {
          title: '.vscode/mcp.json',
          code: json({
            inputs: [
              {
                type: 'promptString',
                id: 'schuly_key',
                description: 'Schuly MCP API key',
                password: true,
              },
            ],
            servers: {
              schuly: {
                type: 'http',
                url,
                headers: { Authorization: 'Bearer ${input:schuly_key}' },
              },
            },
          }),
        },
      },
    },
    {
      id: 'windsurf',
      name: 'Windsurf',
      icon: 'lucideCode',
      brand: 'windsurf',
      steps: [
        'Open ~/.codeium/windsurf/mcp_config.json (or the MCP panel, then Configure).',
        'Add this server.',
        'Click Refresh in the MCP panel and finish the sign-in in your browser.',
      ],
      snippet: {
        title: '~/.codeium/windsurf/mcp_config.json',
        code: json({ mcpServers: { schuly: { serverUrl: url } } }),
      },
      note: 'Windsurf uses "serverUrl" for remote servers.',
      key: {
        intro: 'Add a headers entry and put the key in the SCHULY_MCP_KEY environment variable.',
        snippet: {
          title: '~/.codeium/windsurf/mcp_config.json',
          code: json({
            mcpServers: { schuly: { serverUrl: url, headers: { Authorization: bearerEnv } } },
          }),
        },
      },
    },
    {
      id: 'gemini-cli',
      name: 'Gemini CLI',
      icon: 'lucideTerminal',
      brand: 'gemini',
      steps: [
        'Run this command, or add the server to ~/.gemini/settings.json.',
        'Start Gemini CLI and run /mcp auth schuly.',
        'Finish the sign-in in your browser.',
      ],
      snippet: { title: 'Terminal', code: `gemini mcp add --transport http schuly ${url}` },
      note: `In ~/.gemini/settings.json use "httpUrl", not "url": { "mcpServers": { "schuly": { "httpUrl": "${url}" } } }`,
      key: {
        intro: 'Pass the key as a header when you register the server.',
        snippet: {
          title: 'Terminal',
          code: `gemini mcp add --transport http --header "Authorization: Bearer ${MCP_KEY_PLACEHOLDER}" schuly ${url}`,
        },
      },
    },
    {
      id: 'codex-cli',
      name: 'Codex CLI',
      icon: 'lucideTerminal',
      brand: 'codex',
      steps: [
        'Run these commands, or add the server to ~/.codex/config.toml.',
        'Finish the sign-in in your browser when codex mcp login opens it.',
      ],
      snippet: {
        title: 'Terminal',
        code: `codex mcp add schuly --url ${url}\ncodex mcp login schuly`,
      },
      note: `Or in ~/.codex/config.toml: [mcp_servers.schuly] with url = "${url}".`,
      key: {
        intro: 'Read the key from an environment variable.',
        snippet: {
          title: '~/.codex/config.toml',
          code: `[mcp_servers.schuly]\nurl = "${url}"\nbearer_token_env_var = "SCHULY_MCP_KEY"\n\n# then, in your shell:\nexport SCHULY_MCP_KEY=${MCP_KEY_PLACEHOLDER.replace('YOUR_KEY', '...')}`,
        },
      },
    },
  ];
}
