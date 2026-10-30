import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

@Component({
  selector: 'fb-initials',
  templateUrl: './fb-initials.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FbInitials {
  // Nullable: a Keycloak account (e.g. a GitHub sign-in without a public name) may have no name.
  firstName = input.required<string | null>();
  lastName = input.required<string | null>();
  firstChar = computed(() => (this.firstName() ?? '').charAt(0).toUpperCase());
  lastChar = computed(() => (this.lastName() ?? '').charAt(0).toUpperCase());
}
