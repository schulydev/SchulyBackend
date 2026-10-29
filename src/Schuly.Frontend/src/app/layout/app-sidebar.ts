import { Component, computed, inject } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideCalendarDays, lucideChartColumn, lucideClipboardList, lucideFileText, lucideHardDrive, lucideLayoutDashboard, lucideMail, lucideMessagesSquare, lucideNotebook, lucideSettings, lucideUserX } from '@ng-icons/lucide';
import { HlmSidebarImports } from '@spartan-ng/helm/sidebar';
import { MicrosoftAccountsStore } from '../stores/microsoft-accounts.store';
import { NavUser } from './nav-user';

interface NavItem {
  title: string;
  url: string;
  icon: string;
}

@Component({
  selector: 'app-sidebar',
  imports: [HlmSidebarImports, NgIcon, RouterLink, RouterLinkActive, NavUser],
  providers: [provideIcons({ lucideLayoutDashboard, lucideChartColumn, lucideClipboardList, lucideCalendarDays, lucideUserX, lucideFileText, lucideHardDrive, lucideMail, lucideMessagesSquare, lucideNotebook, lucideSettings })],
  template: `
    <hlm-sidebar collapsible="icon">
      <div hlmSidebarHeader class="border-b">
        <ul hlmSidebarMenu>
          <li hlmSidebarMenuItem>
            <a hlmSidebarMenuButton routerLink="/" class="!p-1.5">
              <img src="app_icon.png" alt="" class="size-5 shrink-0 rounded-sm group-data-[collapsible=icon]:size-4" />
              <span class="text-base font-semibold">Schuly</span>
            </a>
          </li>
        </ul>
      </div>
      <div hlmSidebarContent>
        @for (group of groups(); track group.label) {
          <div hlmSidebarGroup>
            <div hlmSidebarGroupLabel>{{ group.label }}</div>
            <div hlmSidebarGroupContent>
              <ul hlmSidebarMenu>
                @for (item of group.items; track item.url) {
                  <li hlmSidebarMenuItem>
                    <a
                      hlmSidebarMenuButton
                      [routerLink]="item.url"
                      routerLinkActive
                      #rla="routerLinkActive"
                      [routerLinkActiveOptions]="{ exact: item.url === '/' }"
                      [isActive]="rla.isActive"
                      [tooltip]="item.title"
                    >
                      <ng-icon [name]="item.icon" />
                      <span>{{ item.title }}</span>
                    </a>
                  </li>
                }
              </ul>
            </div>
          </div>
        }
        <div hlmSidebarGroup class="mt-auto">
          <div hlmSidebarGroupContent>
            <ul hlmSidebarMenu>
              <li hlmSidebarMenuItem>
                <a hlmSidebarMenuButton routerLink="/settings" routerLinkActive #settings="routerLinkActive" [isActive]="settings.isActive" tooltip="Settings">
                  <ng-icon name="lucideSettings" />
                  <span>Settings</span>
                </a>
              </li>
            </ul>
          </div>
        </div>
      </div>
      <div hlmSidebarFooter>
        <app-nav-user />
      </div>
    </hlm-sidebar>
  `,
})
export class AppSidebar {
  private readonly microsoft = inject(MicrosoftAccountsStore);

  private readonly microsoftGroup = {
    label: 'Microsoft 365',
    items: [
      { title: 'Mail', url: '/mail', icon: 'lucideMail' },
      { title: 'Teams', url: '/teams', icon: 'lucideMessagesSquare' },
      { title: 'Drive', url: '/drive', icon: 'lucideHardDrive' },
      { title: 'OneNote', url: '/onenote', icon: 'lucideNotebook' },
    ],
  };

  protected readonly groups = computed<{ label: string; items: NavItem[] }[]>(() => [
    ...this.schoolGroups,
    ...(this.microsoft.hasAccounts() ? [this.microsoftGroup] : []),
  ]);

  private readonly schoolGroups: { label: string; items: NavItem[] }[] = [
    {
      label: 'Home',
      items: [{ title: 'Overview', url: '/', icon: 'lucideLayoutDashboard' }],
    },
    {
      label: 'School',
      items: [
        { title: 'Grades', url: '/grades', icon: 'lucideChartColumn' },
        { title: 'Exams', url: '/exams', icon: 'lucideClipboardList' },
        { title: 'Calendar', url: '/calendar', icon: 'lucideCalendarDays' },
        { title: 'Absences', url: '/absences', icon: 'lucideUserX' },
        { title: 'Documents', url: '/documents', icon: 'lucideFileText' },
      ],
    },
  ];
}
