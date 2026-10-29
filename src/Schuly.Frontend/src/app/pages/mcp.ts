import { DOCUMENT } from '@angular/common';
import { Component, inject } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideChevronDown } from '@ng-icons/lucide';
import { HlmButton } from '@spartan-ng/helm/button';
import { HlmCardImports } from '@spartan-ng/helm/card';
import { HlmCollapsibleImports } from '@spartan-ng/helm/collapsible';
import { mcpUrl } from '../core/mcp-clients';
import { CopyBlock } from '../shared/copy-block';
import { McpClientsCard } from './mcp/mcp-clients-card';
import { McpConnectionsCard } from './mcp/mcp-connections-card';
import { McpKeysCard } from './mcp/mcp-keys-card';

@Component({
  selector: 'app-mcp',
  imports: [HlmButton, HlmCardImports, HlmCollapsibleImports, NgIcon, CopyBlock, McpClientsCard, McpConnectionsCard, McpKeysCard],
  providers: [provideIcons({ lucideChevronDown })],
  template: `
    <div class="mx-auto flex w-full max-w-3xl flex-col gap-6 p-4 lg:p-6">
      <p class="text-muted-foreground text-sm">
        Connect AI assistants like Claude, ChatGPT and Cursor to your Schuly data.
      </p>

      <section hlmCard>
        <div hlmCardHeader>
          <h2 hlmCardTitle>Server URL</h2>
          <p hlmCardDescription>Add this address to your AI app as a remote MCP server.</p>
        </div>
        <div hlmCardContent>
          <app-copy-block [text]="url" label="Server URL" [single]="true" />
        </div>
      </section>

      <app-mcp-clients-card />
      <app-mcp-connections-card />
      <app-mcp-keys-card />

      <section hlmCard>
        <hlm-collapsible>
          <div hlmCardHeader>
            <h2 hlmCardTitle>
              <button hlmCollapsibleTrigger hlmBtn variant="ghost" class="-ml-2.5 text-base font-semibold">
                Troubleshooting
                <ng-icon name="lucideChevronDown" aria-hidden="true" />
              </button>
            </h2>
          </div>
          <hlm-collapsible-content>
            <div hlmCardContent>
              <ul class="flex list-disc flex-col gap-2 ps-5 text-sm">
                <li>
                  To start over, clear the stored sign-in and connect again. In Claude Code run /mcp and choose Clear
                  authentication.
                </li>
                <li>After you edit a config file, restart or refresh the client so it reads the change.</li>
                <li>
                  A 401 error or "Needs login" means the browser sign-in is not finished yet. Complete it, then try
                  again.
                </li>
                <li>
                  Use the exact key name your client expects: type http, servers or mcpServers, serverUrl or httpUrl.
                </li>
                <li>Cloud clients such as ChatGPT and claude.ai can only reach a public URL, not localhost.</li>
                <li>Revoked or expired API keys stop working immediately.</li>
                <li>
                  If a tool is missing, the permissions of that connection do not allow it. Edit its permissions above.
                </li>
              </ul>
            </div>
          </hlm-collapsible-content>
        </hlm-collapsible>
      </section>
    </div>
  `,
})
export class Mcp {
  protected readonly url = mcpUrl(inject(DOCUMENT).location.origin);
}
