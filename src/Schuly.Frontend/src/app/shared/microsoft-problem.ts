import { Component, computed, input } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideTriangleAlert } from '@ng-icons/lucide';
import { HlmAlertImports } from '@spartan-ng/helm/alert';
import { MicrosoftProblem as Problem, microsoftProblemText } from '../stores/microsoft-resource';

@Component({
  selector: 'app-microsoft-problem',
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
export class MicrosoftProblem {
  readonly problem = input.required<Problem>();
  protected readonly text = computed(() => microsoftProblemText[this.problem()]);
}
