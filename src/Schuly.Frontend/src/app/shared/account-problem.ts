import { Component, computed, input } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideTriangleAlert } from '@ng-icons/lucide';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { AccountProblem as Problem, accountProblemText } from '../stores/account-resource';

@Component({
  selector: 'app-account-problem',
  imports: [HlmAlertImports, NgIcon],
  providers: [provideIcons({ lucideTriangleAlert })],
  template: `
    <div hlmAlert variant="destructive">
      <ng-icon name="lucideTriangleAlert" hlmAlertIcon />
      <h3 hlmAlertTitle>{{ text().title }}</h3>
      <p hlmAlertDescription>{{ text().description }}</p>
    </div>
  `,
})
export class AccountProblem {
  readonly problem = input.required<Problem>();
  protected readonly text = computed(() => accountProblemText[this.problem()]);
}
