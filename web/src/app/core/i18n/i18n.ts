import { HttpClient } from '@angular/common/http';
import { Injectable, Pipe, PipeTransform, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Translation, TranslocoLoader, TranslocoService, provideTransloco } from '@jsverse/transloco';
import { isDevMode } from '@angular/core';

export const IDIOMAS = ['es', 'en'] as const;
export type Idioma = (typeof IDIOMAS)[number];
const LOCALE: Record<Idioma, string> = { es: 'es-MX', en: 'en-US' };

@Injectable({ providedIn: 'root' })
export class CargadorTraducciones implements TranslocoLoader {
  private readonly http = inject(HttpClient);
  getTranslation(idioma: string) {
    return this.http.get<Translation>(`/i18n/${idioma}.json`);
  }
}

/** Caso 8: cambia de idioma sin recargar (Transloco re-renderiza al cambiar). */
export const proveedoresI18n = () =>
  provideTransloco({
    config: { availableLangs: [...IDIOMAS], defaultLang: 'es', fallbackLang: 'es', reRenderOnLangChange: true, prodMode: !isDevMode() },
    loader: CargadorTraducciones,
  });

/** Dinero en MXN con el formato del idioma activo: $23,180.00 en ambos, con separadores locales. */
@Pipe({ name: 'dinero', pure: false })
export class DineroPipe implements PipeTransform {
  private readonly idioma = toSignal(inject(TranslocoService).langChanges$, { initialValue: 'es' });
  transform(valor: number | null | undefined): string {
    if (valor == null) return '—';
    return new Intl.NumberFormat(LOCALE[this.idioma() as Idioma] ?? 'es-MX', { style: 'currency', currency: 'MXN' }).format(valor);
  }
}

/** Fecha corta con el formato del idioma activo (19 nov 2026 · Nov 19, 2026). */
@Pipe({ name: 'fecha', pure: false })
export class FechaPipe implements PipeTransform {
  private readonly idioma = toSignal(inject(TranslocoService).langChanges$, { initialValue: 'es' });
  transform(valor: Date | null | undefined): string {
    if (!valor) return '—';
    return new Intl.DateTimeFormat(LOCALE[this.idioma() as Idioma] ?? 'es-MX', { dateStyle: 'medium' }).format(valor);
  }
}
