import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FbIconButton } from './fb-icon-button';

describe('FbIconButton', () => {
  let component: FbIconButton;
  let fixture: ComponentFixture<FbIconButton>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FbIconButton],
    }).compileComponents();

    fixture = TestBed.createComponent(FbIconButton);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  function classes(): string {
    return (fixture.nativeElement as HTMLElement).className;
  }

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('defaults to the default intent', () => {
    expect(classes()).toContain('text-muted-foreground');
  });

  describe('surface intent (#479)', () => {
    beforeEach(async () => {
      fixture.componentRef.setInput('intent', 'surface');
      await fixture.whenStable();
    });

    it('uses the elevated card surface treatment', () => {
      const surfaceClasses = classes();

      expect(surfaceClasses).toContain('bg-card');
      expect(surfaceClasses).toContain('text-foreground');
      expect(surfaceClasses).toContain('border-[0.5px]');
      expect(surfaceClasses).toContain('border-line');
    });

    it('keeps its hairline border visible', () => {
      // `border-none` also sets Tailwind v4's `--tw-border-style: none`, which
      // `border-[0.5px]` reads back, so the outline vanished and the card-on-card
      // button (and its hover radius change) became invisible.
      expect(classes()).not.toContain('border-none');
    });

    it('contracts its rounded corners on hover', () => {
      expect(classes()).toContain('rounded-[1.25rem]');
      expect(classes()).toContain('hover:rounded-xl');
    });
  });

  describe('destructive intent (#406)', () => {
    beforeEach(async () => {
      fixture.componentRef.setInput('intent', 'destructive');
      await fixture.whenStable();
    });

    it('paints the icon with the destructive text token', () => {
      expect(classes()).toContain('text-destructive-text');
    });

    it('does not fall back to the muted foreground colour', () => {
      // The bug: `default`'s `text-muted-foreground` competing with a
      // caller-supplied `text-destructive-text` left the icon washed out.
      expect(classes()).not.toContain('text-muted-foreground');
    });

    it('uses the opaque destructive-soft hover fill, not an alpha fill (#370)', () => {
      expect(classes()).toContain('hover:bg-destructive-soft');
      expect(classes()).not.toMatch(/bg-destructive\/\d/);
    });

    it('keeps the destructive colour on hover', () => {
      expect(classes()).toContain('hover:text-destructive-text');
    });

    it('bakes in its own pill radius so callers need no radius override', () => {
      expect(classes()).toContain('rounded-full');
    });

    it('keeps its colour when the caller adds a layout-only class', async () => {
      // The caller should only ever need spacing utilities; the variant stays
      // the single source of truth for colour and radius.
      fixture.componentRef.setInput('class', 'mr-2');
      await fixture.whenStable();
      expect(classes()).toContain('text-destructive-text');
      expect(classes()).toContain('mr-2');
    });
  });
});
