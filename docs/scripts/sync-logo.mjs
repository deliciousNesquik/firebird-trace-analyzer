// Синхронизирует логотип сайта с каноническим файлом приложения:
// FirebirdTraceAnalyzer/Assets/app-logo.svg -> docs/public/favicon.svg.
// Вызывается автоматически хуками predev/prebuild (см. package.json) -
// единственный писатель этого файла, ручные правки будут перезаписаны.
import { copyFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const src = resolve(here, '../../FirebirdTraceAnalyzer/Assets/app-logo.svg');
const dst = resolve(here, '../public/favicon.svg');

mkdirSync(dirname(dst), { recursive: true });
copyFileSync(src, dst);
console.log(`[sync-logo] favicon.svg <- ${src}`);
