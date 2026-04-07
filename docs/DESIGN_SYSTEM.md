# Design System - La Llama Del Bosque

## Objetivo
Unificar el diseño visual en todas las pantallas con un estilo moderno, elegante y consistente.

## Tokens globales
Definidos en `wwwroot/css/site.css` dentro de `:root`:
- **Colores de marca:** `--primary`, `--primary-strong`, `--secondary`.
- **Colores de estado:** `--success`, `--warning`, `--danger`.
- **Superficies:** `--background`, `--surface`, `--surface-soft`, `--layout`.
- **Tipografía:** `Inter` para UI y `Playfair Display` para headings.
- **Espaciado visual:** `--radius-*`, `--shadow-*`, `--border-color`.

## Componentes base estandarizados
- **Shell de app:** `app-body`, `app-shell`, `app-main`.
- **Navegación:** `app-navbar`.
- **Footer:** `app-footer`.
- **Botones:** estilo consistente por `btn`, `btn-primary`, `btn-success`, `btn-danger`.
- **Form controls:** `form-control`, `form-select`, `input-group-text` con focus homogéneo.
- **Data display:** `card`, `table`, `badge`, `alert` con bordes y radios consistentes.

## Utilidades de sistema
Clases reutilizables para pantallas nuevas:
- `ds-section-title`
- `ds-kpi`
- `ds-kpi-label`
- `ds-kpi-value`

## Regla de uso
Al crear o editar vistas:
1. Usar layout global (`_Layout.cshtml`) con `app-body`, `app-shell`, `app-main`.
2. Priorizar Bootstrap + clases del sistema (`ds-*`) antes de crear estilos inline.
3. Mantener tokens (`var(--*)`) para colores/radios/sombras.
4. Evitar colores hardcodeados salvo casos excepcionales.
