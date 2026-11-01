---
paths:
  - "src/web/**/*.ts"
  - "src/web/**/*.html"
  - "src/web/**/*.css"
---

# Frontend Patterns — Faber

**Location:** `src/web/faber-app/`

```text
src/app/
├── core/           # App-wide singletons: auth (session store, guard, CSRF interceptor), layouts
├── shared/         # Reusable standalone components (fb-* prefix), pipes, directives
└── modules/        # Lazy-loaded feature modules
    ├── users/      # Account page (`/account`: profile, sign-in methods, devices, linked accounts, delete)
    ├── resumes/    # Resume builder and catalog
    ├── home/
    └── about/
```

## TypeScript

- Avoid `any`; use `unknown` when type is uncertain

## Angular Patterns

**Use:** standalone components — do NOT set `standalone: true` in decorator (default in Angular v20+) · signals for state (`signal()`, `computed()`, `input()`, `output()`) · `inject()` instead of constructor injection · `ChangeDetectionStrategy.OnPush` on all components · native control flow (`@if`/`@for`/`@switch`) · Reactive Forms over template-driven · lazy loading for feature routes · `providedIn: 'root'` for singleton services · `fb-` selector prefix for shared reusable components · `NgOptimizedImage` for all static images · `ng generate` for all scaffolding — never create files manually · always use separate `.html` and `.css` files — no inline templates or styles.

**Avoid:** complex template logic (move to `computed()` or methods) · arrow functions in templates · `@HostBinding`/`@HostListener` (use `host` in component decorator) · constructor for general initialization (use `ngOnInit()`; constructor reserved for `effect()`, `afterNextRender()`, `afterRender()`) · complex lifecycle hooks (extract into well-named methods).

## State Management

No external library (no NgRx). Signal-based reactive state in services:

```typescript
private readonly _user = signal<User | null>(null);
readonly user = this._user.asReadonly();
readonly authenticated = computed(() => !!this._user());
```

## HTTP Patterns

- **`httpResource()`** — declarative reads (GET), loading/error state tracked as signals automatically
- **`HttpClient` + Observable** — imperative mutations (POST, PUT, PATCH, DELETE)

```typescript
// Declarative read
readonly resumes = httpResource<Resume[]>(() => '/api/resumes');

// Imperative mutation
save(data: CreateResumeRequest): Observable<Resume> {
  return this.http.post<Resume>('/api/resumes', data);
}
```

## Accessibility

- Must pass all AXE checks
- Must follow WCAG AA: focus management, color contrast, ARIA attributes

---
Expanded guidance and examples: `angular-component`, `angular-signals`, `angular-http`, `angular-forms`, `angular-routing`, `angular-di`, `angular-testing`.
