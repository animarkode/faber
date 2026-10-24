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

  it('gives surface icon buttons a muted fill so they stand out from the card', () => {
    const classes = (fixture.nativeElement as HTMLElement).className;

    expect(classes).toContain('[--icon-button-surface:var(--color-muted)]');
  });
});
