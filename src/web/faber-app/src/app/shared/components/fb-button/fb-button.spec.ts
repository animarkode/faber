import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FbButton } from './fb-button';

describe('FbButton', () => {
  let component: FbButton;
  let fixture: ComponentFixture<FbButton>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FbButton],
    }).compileComponents();

    fixture = TestBed.createComponent(FbButton);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('destructive fills use opaque theme tokens (#370)', () => {
    // Alpha fills (bg-destructive/10 etc.) composite differently over light vs
    // dark backing — the variants must use the per-theme opaque tokens instead.
    it('soft-destructive uses the destructive-soft token pair', async () => {
      fixture.componentRef.setInput('variant', 'soft-destructive');
      await fixture.whenStable();
      const classes = (fixture.nativeElement as HTMLElement).className;
      expect(classes).toContain('before:bg-destructive-soft');
      expect(classes).toContain('hover:before:bg-destructive-soft-hover');
      expect(classes).not.toMatch(/bg-destructive\/\d/);
    });

    it('destructive hover uses the destructive-hover token', async () => {
      fixture.componentRef.setInput('variant', 'destructive');
      await fixture.whenStable();
      const classes = (fixture.nativeElement as HTMLElement).className;
      expect(classes).toContain('hover:before:bg-destructive-hover');
      expect(classes).not.toMatch(/bg-destructive\/\d/);
    });
  });

  it('menu matches the navbar links: surface fill that shrinks and darkens on hover', async () => {
    fixture.componentRef.setInput('variant', 'menu');
    await fixture.whenStable();
    const classes = (fixture.nativeElement as HTMLElement).className;

    expect(classes).toContain('before:bg-surface');
    expect(classes).toContain('text-foreground/80');
    expect(classes).toContain('hover:before:scale-[0.955]');
    expect(classes).toContain('hover:before:bg-[color-mix(in_oklch,var(--color-surface)_80%,var(--color-foreground))]');
  });
});
