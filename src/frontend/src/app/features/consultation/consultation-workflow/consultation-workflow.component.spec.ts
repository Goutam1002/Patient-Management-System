import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { Visit } from '../consultation.service';
import { ConsultationWorkflowComponent } from './consultation-workflow.component';

const recordedVisit: Visit = {
  id: 9,
  patientId: 5,
  appointmentId: 3,
  visitNumber: 1,
  temperature: 37,
  bpSystolic: 120,
  bpDiastolic: 80,
  pulse: 72,
  weight: 52.85,
  complaints: 'Cough',
  diagnosis: 'URI',
};

function fillVitals(fixture: ComponentFixture<ConsultationWorkflowComponent>): void {
  fixture.componentInstance.vitalsGroup.setValue({
    temperature: '37',
    bpSystolic: '120',
    bpDiastolic: '80',
    pulse: '72',
    weight: '52.85',
  });
}

describe('ConsultationWorkflowComponent -- create mode (start-consultation)', () => {
  let fixture: ComponentFixture<ConsultationWorkflowComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConsultationWorkflowComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ appointmentId: '3' }) } },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ConsultationWorkflowComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => httpMock.verify());

  it('blocks submit when vitals are missing -- no request is issued', () => {
    fixture.componentInstance.submit();

    httpMock.expectNone(`${environment.apiUrl}/appointments/3/start-consultation`);
    expect(fixture.componentInstance.vitalsGroup.invalid).toBeTrue();
    expect(fixture.componentInstance.saving()).toBeFalse();
  });

  it('records vitals + complaints + diagnosis in a single request, one screen, no extra round-trip', () => {
    fillVitals(fixture);
    fixture.componentInstance.complaintsControl.setValue('Cough');
    fixture.componentInstance.diagnosisControl.setValue('URI');
    fixture.componentInstance.submit();

    // Exactly one request for the whole consultation -- httpMock.verify() in
    // afterEach fails if a second one (e.g. a separate save step) was made.
    const req = httpMock.expectOne(`${environment.apiUrl}/appointments/3/start-consultation`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      temperature: 37,
      bpSystolic: 120,
      bpDiastolic: 80,
      pulse: 72,
      weight: 52.85,
      complaints: 'Cough',
      diagnosis: 'URI',
    });
    req.flush(recordedVisit);
    fixture.detectChanges();

    expect(fixture.componentInstance.visit()?.visitNumber).toBe(1);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Visit #1 recorded');
  });

  it('explains a slot/visit conflict when the API returns 409', () => {
    fillVitals(fixture);
    fixture.componentInstance.submit();

    httpMock
      .expectOne(`${environment.apiUrl}/appointments/3/start-consultation`)
      .flush({ detail: 'conflict' }, { status: 409, statusText: 'Conflict' });

    expect(fixture.componentInstance.errorMessage()).toContain('already has a visit');
    expect(fixture.componentInstance.visit()).toBeNull();
  });

  it('shows a generic error for any other failure', () => {
    fillVitals(fixture);
    fixture.componentInstance.submit();

    httpMock
      .expectOne(`${environment.apiUrl}/appointments/3/start-consultation`)
      .flush({ message: 'error' }, { status: 500, statusText: 'Server Error' });

    expect(fixture.componentInstance.errorMessage()).toBe('Could not start the consultation.');
  });
});

describe('ConsultationWorkflowComponent -- edit mode (existing visit)', () => {
  let fixture: ComponentFixture<ConsultationWorkflowComponent>;
  let httpMock: HttpTestingController;

  function createAndLoad(): void {
    fixture = TestBed.createComponent(ConsultationWorkflowComponent);
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/visits/9`).flush(recordedVisit);
    fixture.detectChanges();
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ConsultationWorkflowComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ visitId: '9' }) } },
        },
      ],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('loads the existing visit and disables the vitals group -- vitals are never editable retroactively', () => {
    createAndLoad();

    expect(fixture.componentInstance.vitalsGroup.disabled).toBeTrue();
    expect(fixture.componentInstance.vitalsGroup.getRawValue()).toEqual({
      temperature: '37',
      bpSystolic: '120',
      bpDiastolic: '80',
      pulse: '72',
      weight: '52.85',
    });
    expect(fixture.componentInstance.complaintsControl.value).toBe('Cough');
    expect(fixture.componentInstance.diagnosisControl.value).toBe('URI');

    const temperatureInput = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('#temperature');
    expect(temperatureInput?.disabled).toBeTrue();
  });

  it('submits only complaints/diagnosis on save -- vitals are never sent from this path', () => {
    createAndLoad();

    fixture.componentInstance.complaintsControl.setValue('Cough, worse at night');
    fixture.componentInstance.diagnosisControl.setValue('Bronchitis');
    fixture.componentInstance.submit();

    const req = httpMock.expectOne(`${environment.apiUrl}/visits/9`);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ complaints: 'Cough, worse at night', diagnosis: 'Bronchitis' });
    req.flush({ ...recordedVisit, complaints: 'Cough, worse at night', diagnosis: 'Bronchitis' });
    fixture.detectChanges();

    expect(fixture.componentInstance.savedJustNow()).toBeTrue();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Changes saved.');
  });

  it('shows an error message when loading the visit fails', () => {
    fixture = TestBed.createComponent(ConsultationWorkflowComponent);
    fixture.detectChanges();

    httpMock
      .expectOne(`${environment.apiUrl}/visits/9`)
      .flush({ message: 'error' }, { status: 500, statusText: 'Server Error' });

    expect(fixture.componentInstance.loading()).toBeFalse();
    expect(fixture.componentInstance.errorMessage()).toBe('Could not load this visit.');
  });

  it('shows an error message when saving fails', () => {
    createAndLoad();

    fixture.componentInstance.submit();
    httpMock
      .expectOne(`${environment.apiUrl}/visits/9`)
      .flush({ message: 'error' }, { status: 500, statusText: 'Server Error' });

    expect(fixture.componentInstance.errorMessage()).toBe('Could not save the changes.');
  });
});
