import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AppHeaderComponent } from './shared/app-header/app-header.component';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, AppHeaderComponent],
  templateUrl: './app.component.html',
})
export class AppComponent {
  private readonly router = inject(Router);
  private readonly activatedRoute = inject(ActivatedRoute);

  // Read from the deepest activated route's `data.hideHeader` (see
  // app.routes.ts) rather than matching on the URL string, so a route's
  // header visibility stays declared next to its own definition. Starts
  // hidden: the router hasn't resolved the initial route yet at construction
  // time, so defaulting to "shown" would flash the header for a moment on
  // every hard refresh/deep link, including onto add/edit pages, before the
  // first NavigationEnd corrects it.
  private readonly hideHeader = signal(true);

  readonly showHeader = computed(() => !this.hideHeader());

  constructor() {
    this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(() => this.hideHeader.set(this.deepestRouteDataHideHeader()));
  }

  private deepestRouteDataHideHeader(): boolean {
    let route = this.activatedRoute;
    while (route.firstChild) {
      route = route.firstChild;
    }
    return route.snapshot.data['hideHeader'] === true;
  }
}
