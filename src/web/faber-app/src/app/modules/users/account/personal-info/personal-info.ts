import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { AccountClient } from '../account-client';
import { AccountProfile, ProfileField } from '../account-contracts';
import { humanize } from '../account-labels';
import { FbButton } from '../../../../shared/components/fb-button/fb-button';
import { FbInput } from '../../../../shared/components/fb-input/fb-input';

const CORE_FIELDS = ['firstName', 'lastName', 'email', 'username'];

interface EditableField {
  name: string;
  label: string;
  required: boolean;
}

@Component({
  selector: 'app-personal-info',
  imports: [ReactiveFormsModule, FbButton, FbInput],
  templateUrl: './personal-info.html',
  styleUrl: './personal-info.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PersonalInfo {
  private readonly client = inject(AccountClient);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly profile = this.client.profileResource();

  protected readonly saving = signal(false);
  protected readonly saved = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly serverErrors = signal<Record<string, string[]>>({});

  /** The profile as last read or saved, kept apart from the resource so a save can replace it locally. */
  private readonly current = signal<AccountProfile | null>(null);
  protected readonly account = computed(() => this.current() ?? this.profile.value() ?? null);

  protected readonly extraFields = computed<EditableField[]>(() =>
    (this.account()?.fields ?? [])
      .filter(f => !f.readOnly && !CORE_FIELDS.includes(f.name))
      .map(f => ({ name: f.name, label: humanize(f.displayName) || humanize(f.name), required: f.required })),
  );

  protected readonly form = computed(() => {
    const account = this.account();
    return account ? this.buildForm(account) : null;
  });

  protected save(): void {
    const form = this.form();
    if (!form || this.saving()) return;
    if (form.invalid) {
      form.markAllAsTouched();
      return;
    }
    const { firstName, lastName, ...rest } = form.getRawValue();
    this.saving.set(true);
    this.saved.set(false);
    this.error.set(null);
    this.serverErrors.set({});
    this.client
      .updateProfile({ firstName, lastName, attributes: rest })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: updated => {
          this.current.set(updated);
          this.saving.set(false);
          this.saved.set(true);
        },
        error: (e: unknown) => {
          this.saving.set(false);
          this.showFailure(e);
        },
      });
  }

  protected errorsFor(name: string): string[] {
    const all = this.serverErrors();
    const key = Object.keys(all).find(k => k.toLowerCase() === name.toLowerCase());
    return key ? all[key] : [];
  }

  protected errorId(name: string): string {
    return `personal-info-${name}-error`;
  }

  protected changeEmail(): void {
    this.client.startAction('email');
  }

  private showFailure(e: unknown): void {
    if (e instanceof HttpErrorResponse && e.status === 400) {
      const errors = (e.error as { errors?: Record<string, string[]> } | null)?.errors;
      if (errors && Object.keys(errors).length > 0) {
        this.serverErrors.set(errors);
        this.error.set('Some details need your attention.');
        return;
      }
    }
    this.error.set('Your details could not be saved. Try again.');
  }

  private buildForm(account: AccountProfile) {
    const find = (name: string): ProfileField | undefined => account.fields.find(f => f.name === name);
    const controls: Record<string, FormControl<string>> = {
      firstName: this.control(account.firstName, find('firstName')),
      lastName: this.control(account.lastName, find('lastName')),
    };
    for (const field of account.fields) {
      if (!field.readOnly && !CORE_FIELDS.includes(field.name)) {
        controls[field.name] = this.control(field.value, field);
      }
    }
    return new FormGroup(controls);
  }

  private control(value: string | null, field: ProfileField | undefined): FormControl<string> {
    const validators: ValidatorFn[] = [];
    if (field?.required) validators.push(Validators.required);
    if (field?.minLength) validators.push(Validators.minLength(field.minLength));
    if (field?.maxLength) validators.push(Validators.maxLength(field.maxLength));
    return new FormControl(value ?? '', { nonNullable: true, validators });
  }
}
