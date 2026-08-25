import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DateRangeFilterComponent } from './date-range-filter.component';

describe('DateRangeFilterComponent', () => {
  let fixture: ComponentFixture<DateRangeFilterComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DateRangeFilterComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(DateRangeFilterComponent);
    fixture.detectChanges();
  });

  it('emits the current from/to values on submit', () => {
    const emitted: unknown[] = [];
    fixture.componentInstance.rangeChange.subscribe((range) => emitted.push(range));

    fixture.componentInstance.form.setValue({ from: '2026-01-01', to: '2026-01-31' });
    fixture.componentInstance.apply();

    expect(emitted).toEqual([{ from: '2026-01-01', to: '2026-01-31' }]);
  });

  it('clear resets the form and emits null bounds', () => {
    const emitted: unknown[] = [];
    fixture.componentInstance.rangeChange.subscribe((range) => emitted.push(range));

    fixture.componentInstance.form.setValue({ from: '2026-01-01', to: '2026-01-31' });
    fixture.componentInstance.clear();

    expect(fixture.componentInstance.form.value.from).toBeNull();
    expect(emitted).toEqual([{ from: null, to: null }]);
  });
});
