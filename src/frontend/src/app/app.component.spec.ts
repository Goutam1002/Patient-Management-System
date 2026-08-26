import { Component } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, Routes, provideRouter } from '@angular/router';
import { environment } from '../environments/environment';
import { AppComponent } from './app.component';

@Component({ standalone: true, template: '<p>show-header page</p>' })
class ShowHeaderStubComponent {}

@Component({ standalone: true, template: '<p>hide-header page</p>' })
class HideHeaderStubComponent {}

const testRoutes: Routes = [
  { path: 'shown', component: ShowHeaderStubComponent },
  { path: 'hidden', component: HideHeaderStubComponent, data: { hideHeader: true } },
];

describe('AppComponent', () => {
  let fixture: ComponentFixture<AppComponent>;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideRouter(testRoutes), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(AppComponent);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });

  afterEach(() => httpMock.verify());

  it('does not flash the header before the initial navigation resolves', () => {
    fixture.detectChanges();
    httpMock.expectNone(`${environment.apiUrl}/doctor-details`);
    expect((fixture.nativeElement as HTMLElement).querySelector('nav[aria-label="Main navigation"]')).toBeFalsy();
  });

  it('shows the header on a route without data.hideHeader', async () => {
    fixture.detectChanges();
    await router.navigateByUrl('/shown');
    fixture.detectChanges();
    httpMock.expectOne(`${environment.apiUrl}/doctor-details`).flush({
      id: 0,
      clinicName: '',
      doctorName: '',
      qualifications: null,
      registrationNumber: null,
      logo: null,
      signature: null,
    });
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('nav[aria-label="Main navigation"]')).toBeTruthy();
    expect(root.textContent).toContain('show-header page');
  });

  it('hides the header on a route with data.hideHeader = true', async () => {
    fixture.detectChanges();
    await router.navigateByUrl('/hidden');
    fixture.detectChanges();

    httpMock.expectNone(`${environment.apiUrl}/doctor-details`);
    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('nav[aria-label="Main navigation"]')).toBeFalsy();
    expect(root.textContent).toContain('hide-header page');
  });
});
