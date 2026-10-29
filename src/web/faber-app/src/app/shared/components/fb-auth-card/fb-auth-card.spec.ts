import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FbAuthCard } from './fb-auth-card';

describe('FbAuthCard', () => {
  let component: FbAuthCard;
  let fixture: ComponentFixture<FbAuthCard>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FbAuthCard],
    }).compileComponents();

    fixture = TestBed.createComponent(FbAuthCard);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('has no card fill of its own', () => {
    const classes = (fixture.nativeElement as HTMLElement).className;

    expect(classes).not.toContain('bg-card');
  });

  it('fills its fields with the card colour so they stand out from the page', () => {
    const classes = (fixture.nativeElement as HTMLElement).className;

    expect(classes).toContain('[--color-input:var(--color-card)]');
  });
});
