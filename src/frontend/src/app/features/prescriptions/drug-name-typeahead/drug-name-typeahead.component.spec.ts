import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { FormControl } from '@angular/forms';
import { environment } from '../../../../environments/environment';
import { DrugNameTypeaheadComponent } from './drug-name-typeahead.component';

describe('DrugNameTypeaheadComponent', () => {
  let fixture: ComponentFixture<DrugNameTypeaheadComponent>;
  let httpMock: HttpTestingController;
  let control: FormControl<string>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DrugNameTypeaheadComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(DrugNameTypeaheadComponent);
    control = new FormControl<string>('', { nonNullable: true });
    fixture.componentRef.setInput('control', control);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('renders an input bound to the supplied control', () => {
    control.setValue('Amox');
    fixture.detectChanges();

    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('input');
    expect(input?.value).toBe('Amox');
  });

  it('fetches suggestions (debounced) as the doctor types and renders them in a dropdown', fakeAsync(() => {
    fixture.componentInstance.onInput('oxic');
    tick(200);

    const req = httpMock.expectOne(`${environment.apiUrl}/prescriptions/drug-suggestions?prefix=oxic`);
    req.flush(['Amoxicillin']);
    fixture.detectChanges();

    const items = (fixture.nativeElement as HTMLElement).querySelectorAll('li');
    expect(items.length).toBe(1);
    expect(items[0].textContent?.trim()).toBe('Amoxicillin');
  }));

  it('does not call the API for a blank term', fakeAsync(() => {
    fixture.componentInstance.onInput('');
    tick(200);

    httpMock.expectNone(`${environment.apiUrl}/prescriptions/drug-suggestions?prefix=`);
    expect(fixture.componentInstance.suggestions()).toEqual([]);
  }));

  it('selecting a suggestion sets the control value and closes the dropdown', fakeAsync(() => {
    fixture.componentInstance.onInput('oxic');
    tick(200);
    httpMock.expectOne(`${environment.apiUrl}/prescriptions/drug-suggestions?prefix=oxic`).flush(['Amoxicillin']);
    fixture.detectChanges();

    fixture.componentInstance.select('Amoxicillin');
    fixture.detectChanges();

    expect(control.value).toBe('Amoxicillin');
    expect(fixture.componentInstance.suggestionsOpen()).toBeFalse();
  }));
});
