import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FbInput } from './fb-input';
import { expectNoAxeViolations } from '../../testing/axe';

@Component({
  template: `<fb-input [formControl]="ctrl" [label]="label" [type]="type"
                       [placeholder]="placeholder" [showToggle]="showToggle" [icon]="icon" />`,
  imports: [FbInput, ReactiveFormsModule],
})
class TestHost {
  ctrl = new FormControl('', Validators.required);
  label = 'Email';
  type = 'text';
  placeholder = 'Enter email';
  showToggle = false;
  icon: 'user' | 'email' | 'password' | 'lock' | null = null;
}

@Component({
  template: `
    <form [formGroup]="form">
      <fb-input formControlName="password" label="Password" />
      <fb-input formControlName="confirmPassword" label="Confirm" />
    </form>
  `,
  imports: [FbInput, ReactiveFormsModule],
})
class MismatchHost {
  // A cross-field confirm check reports its failure as a `mismatch` error on the confirm control.
  form = new FormGroup({
    password: new FormControl('abc12345'),
    confirmPassword: new FormControl('different', () => ({ mismatch: true })),
  });
}

@Component({
  template: `<fb-input [formControl]="ctrl" label="Username" />`,
  imports: [FbInput, ReactiveFormsModule],
})
class MinlengthHost {
  ctrl = new FormControl('ab', Validators.minLength(8));
}

@Component({
  template: `<fb-input [formControl]="ctrl" label="Email" />`,
  imports: [FbInput, ReactiveFormsModule],
})
class EmailHost {
  ctrl = new FormControl('notanemail', Validators.email);
}

describe('FbInput', () => {
  let hostFixture: ComponentFixture<TestHost>;
  let host: TestHost;

  beforeEach(async () => {
    vi.clearAllMocks();

    await TestBed.configureTestingModule({
      imports: [TestHost, MismatchHost, MinlengthHost, EmailHost],
    }).compileComponents();

    hostFixture = TestBed.createComponent(TestHost);
    host = hostFixture.componentInstance;
  });

  // --- Rendering ---

  it('should create', () => {
    expect(hostFixture.componentInstance).toBeTruthy();
  });

  it('should render the label', () => {
    hostFixture.detectChanges();
    const label = hostFixture.nativeElement.querySelector('label');
    expect(label?.textContent?.trim()).toBe('Email');
  });

  it('should not render label when label input is empty', async () => {
    host.label = '';
    hostFixture.detectChanges();
    await hostFixture.whenStable();
    const label = hostFixture.nativeElement.querySelector('label');
    expect(label).toBeNull();
  });

  // Component uses .fb-icon class on icon wrapper span
  it('should render icon element when icon input is provided', () => {
    host.icon = 'email';
    hostFixture.detectChanges();
    const icon = hostFixture.nativeElement.querySelector('.fb-icon');
    expect(icon).toBeTruthy();
  });

  it('should not render icon element when icon input is null', () => {
    host.icon = null;
    hostFixture.detectChanges();
    const icon = hostFixture.nativeElement.querySelector('.fb-icon');
    expect(icon).toBeNull();
  });

  it('should render placeholder on native input', () => {
    hostFixture.detectChanges();
    const input = hostFixture.nativeElement.querySelector('input');
    expect(input?.placeholder).toBe('Enter email');
  });

  it('should use 16px text on a phone so iOS Safari does not zoom in on focus', () => {
    hostFixture.detectChanges();
    const input = hostFixture.nativeElement.querySelector('input');
    expect(input?.className).toContain('max-sm:text-base');
  });

  // --- CVA: value binding ---

  it('should display value set via writeValue', async () => {
    host.ctrl.setValue('hello@test.com');
    hostFixture.detectChanges();
    await hostFixture.whenStable();
    const input = hostFixture.nativeElement.querySelector('input') as HTMLInputElement;
    expect(input.value).toBe('hello@test.com');
  });

  it('should call onChange when user types', () => {
    hostFixture.detectChanges();
    const input = hostFixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = 'typed';
    input.dispatchEvent(new Event('input'));
    expect(host.ctrl.value).toBe('typed');
  });

  it('should mark control touched on blur', () => {
    hostFixture.detectChanges();
    const input = hostFixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.dispatchEvent(new Event('focus'));
    input.dispatchEvent(new Event('blur'));
    hostFixture.detectChanges();
    expect(host.ctrl.touched).toBe(true);
  });

  // --- Error states ---

  it('should not show error message when untouched', () => {
    hostFixture.detectChanges();
    const error = hostFixture.nativeElement.querySelector('.fb-error');
    expect(error).toBeNull();
  });

  it('should show required error after touch', async () => {
    host.ctrl.markAsTouched();
    hostFixture.detectChanges();
    await hostFixture.whenStable();
    const error = hostFixture.nativeElement.querySelector('.fb-error');
    expect(error?.textContent?.trim()).toBe('Email is required.');
  });

  it('should show minlength error', async () => {
    const fixture = TestBed.createComponent(MinlengthHost);
    fixture.componentInstance.ctrl.markAsTouched();
    fixture.detectChanges();
    await fixture.whenStable();
    const error = fixture.nativeElement.querySelector('.fb-error');
    expect(error?.textContent?.trim()).toBe('Must be at least 8 characters.');
  });

  it('should show email error', async () => {
    const fixture = TestBed.createComponent(EmailHost);
    fixture.componentInstance.ctrl.markAsTouched();
    fixture.detectChanges();
    await fixture.whenStable();
    const error = fixture.nativeElement.querySelector('.fb-error');
    expect(error?.textContent?.trim()).toBe('Must be a valid email.');
  });

  it('should show mismatch error on confirmPassword field', async () => {
    const mismatchFixture = TestBed.createComponent(MismatchHost);
    const mismatchHost = mismatchFixture.componentInstance;
    mismatchHost.form.controls.confirmPassword.markAsTouched();
    mismatchFixture.detectChanges();
    await mismatchFixture.whenStable();

    const errors = Array.from<Element>(mismatchFixture.nativeElement.querySelectorAll('.fb-error'));
    const messages = errors.map(el => el.textContent?.trim());
    expect(messages).toContain('Passwords do not match.');
  });

  it('should set aria-invalid when error', async () => {
    host.ctrl.markAsTouched();
    hostFixture.detectChanges();
    await hostFixture.whenStable();
    const input = hostFixture.nativeElement.querySelector('input');
    expect(input?.getAttribute('aria-invalid')).toBe('true');
  });

  it('should apply error CSS class on wrapper when error', async () => {
    host.ctrl.markAsTouched();
    hostFixture.detectChanges();
    await hostFixture.whenStable();
    const wrap = hostFixture.nativeElement.querySelector('.fb-input-wrap');
    expect(wrap?.classList.contains('fb-input-wrap--error')).toBe(true);
  });

  // --- Password toggle ---

  it('should not show toggle button when showToggle is false', () => {
    host.type = 'password';
    host.showToggle = false;
    hostFixture.detectChanges();
    const toggle = hostFixture.nativeElement.querySelector('.fb-toggle');
    expect(toggle).toBeNull();
  });

  it('should not show toggle button when type is not password', () => {
    host.type = 'text';
    host.showToggle = true;
    hostFixture.detectChanges();
    const toggle = hostFixture.nativeElement.querySelector('.fb-toggle');
    expect(toggle).toBeNull();
  });

  it('should show toggle button when showToggle=true and type=password', () => {
    host.type = 'password';
    host.showToggle = true;
    hostFixture.detectChanges();
    const toggle = hostFixture.nativeElement.querySelector('.fb-toggle');
    expect(toggle).toBeTruthy();
  });

  it('should switch input type to text when toggle is clicked', async () => {
    host.type = 'password';
    host.showToggle = true;
    hostFixture.detectChanges();
    const toggle = hostFixture.nativeElement.querySelector('.fb-toggle') as HTMLButtonElement;
    toggle.click();
    hostFixture.detectChanges();
    await hostFixture.whenStable();
    const input = hostFixture.nativeElement.querySelector('input') as HTMLInputElement;
    expect(input.type).toBe('text');
  });

  it('should switch input type back to password on second toggle click', async () => {
    host.type = 'password';
    host.showToggle = true;
    hostFixture.detectChanges();
    const toggle = hostFixture.nativeElement.querySelector('.fb-toggle') as HTMLButtonElement;
    toggle.click();
    toggle.click();
    hostFixture.detectChanges();
    await hostFixture.whenStable();
    const input = hostFixture.nativeElement.querySelector('input') as HTMLInputElement;
    expect(input.type).toBe('password');
  });

  // --- Focus label (issue #373: no colour change on focus) ---

  it('should keep label colour unchanged when focused', () => {
    hostFixture.detectChanges();
    const input = hostFixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.dispatchEvent(new Event('focus'));
    hostFixture.detectChanges();

    const label = hostFixture.nativeElement.querySelector('label') as HTMLLabelElement;
    expect(label.classList.contains('text-warning-text')).toBe(false);
    expect(label.classList.contains('text-muted-foreground')).toBe(true);
  });

  it('should still show destructive label colour when focused with error', async () => {
    host.ctrl.markAsTouched();
    hostFixture.detectChanges();
    await hostFixture.whenStable();
    const input = hostFixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.dispatchEvent(new Event('focus'));
    hostFixture.detectChanges();

    const label = hostFixture.nativeElement.querySelector('label') as HTMLLabelElement;
    expect(label.classList.contains('text-destructive-text')).toBe(true);
  });

  // --- Accessibility ---

  it('should have no AXE violations in error state', async () => {
    host.ctrl.markAsTouched();
    hostFixture.detectChanges();
    await hostFixture.whenStable();
    await expectNoAxeViolations(hostFixture);
  }, 20000);

  it('should have no AXE violations in error state in dark mode', async () => {
    document.documentElement.classList.add('dark');
    try {
      host.ctrl.markAsTouched();
      hostFixture.detectChanges();
      await hostFixture.whenStable();
      await expectNoAxeViolations(hostFixture);
    } finally {
      document.documentElement.classList.remove('dark');
    }
  }, 20000);
});
