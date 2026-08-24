import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { environment } from '../../../../environments/environment';
import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let httpMock: HttpTestingController;

  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();

    fixture = TestBed.createComponent(LoginComponent);
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('does not submit an incomplete form', () => {
    fixture.componentInstance.submit();
    httpMock.expectNone(`${environment.apiUrl}/auth/login`);
    expect(fixture.componentInstance.form.touched).toBeTrue();
  });

  it('navigates away on successful login', () => {
    const router = TestBed.inject(Router);
    spyOn(router, 'navigateByUrl');

    fixture.componentInstance.form.setValue({ username: 'doctor', password: 'ChangeMe123!' });
    fixture.componentInstance.submit();

    httpMock.expectOne(`${environment.apiUrl}/auth/login`).flush({ username: 'doctor', sessionToken: 'abc123' });

    expect(router.navigateByUrl).toHaveBeenCalledWith('/');
  });

  it('shows an error message when login fails', () => {
    fixture.componentInstance.form.setValue({ username: 'doctor', password: 'wrong' });
    fixture.componentInstance.submit();

    httpMock.expectOne(`${environment.apiUrl}/auth/login`).flush(
      { message: 'Unauthorized' },
      { status: 401, statusText: 'Unauthorized' },
    );

    expect(fixture.componentInstance.errorMessage()).toBe('Incorrect username or password.');
  });
});
