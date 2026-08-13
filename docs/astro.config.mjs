// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

// Проект-сайт GitHub Pages: https://deliciousnesquik.github.io/firebird-trace-analyzer/
// site + base обязательны, чтобы ссылки/ассеты строились с префиксом репозитория.
// https://astro.build/config
export default defineConfig({
  site: 'https://deliciousnesquik.github.io',
  base: '/firebird-trace-analyzer',
  integrations: [
    starlight({
      title: 'Firebird Trace Analyzer Docs',
      logo: {
        light: './src/assets/app-logo-light.svg',
        dark: './src/assets/app-logo-dark.svg',
        alt: 'Firebird Trace Analyzer',
      },
      description:
        'Cross-platform desktop toolkit for reading, exploring and reporting on Firebird trace & audit logs.',
      lastUpdated: true,
      // Двуязычная документация. root = английский (страницы лежат в корне content/docs без префикса),
      // ru = русский (страницы в content/docs/ru/**). Непереведённые страницы автоматически
      // откатываются на английский контент по тому же URL (встроенный fallback Starlight).
      locales: {
        root: { label: 'English', lang: 'en' },
        ru: { label: 'Русский', lang: 'ru' },
      },
      customCss: ['./src/styles/landing.css'],
      social: [
        {
          icon: 'github',
          label: 'GitHub',
          href: 'https://github.com/deliciousNesquik/firebird-trace-analyzer',
        },
      ],
      sidebar: [
        {
          label: 'Getting started',
          translations: { ru: 'Начало работы' },
          items: [
            { label: 'Overview', translations: { ru: 'Обзор' }, slug: 'getting-started/overview' },
            { label: 'Install & run', translations: { ru: 'Установка и запуск' }, slug: 'getting-started/install' },
          ],
        },
        {
          label: 'Guides',
          translations: { ru: 'Руководства' },
          items: [
            { label: 'Loading logs', translations: { ru: 'Загрузка логов' }, slug: 'guides/loading-logs' },
            { label: 'Exploring & filtering', translations: { ru: 'Просмотр и фильтрация' }, slug: 'guides/exploring' },
            { label: 'Local event store', translations: { ru: 'Локальное хранилище событий' }, slug: 'guides/event-store' },
            { label: 'Reports', translations: { ru: 'Отчёты' }, slug: 'guides/reports' },
            { label: 'Plugins', translations: { ru: 'Плагины' }, slug: 'guides/plugins' },
          ],
        },
        {
          label: 'SDK',
          translations: { ru: 'SDK' },
          items: [
            { label: 'Overview', translations: { ru: 'Обзор' }, slug: 'sdk/overview' },
            { label: 'Sort plugins', translations: { ru: 'Плагины сортировки' }, slug: 'sdk/sort-plugins' },
            { label: 'Filter plugins', translations: { ru: 'Плагины фильтрации' }, slug: 'sdk/filter-plugins' },
            { label: 'Event model', translations: { ru: 'Модель событий' }, slug: 'sdk/event-model' },
            { label: 'Building & loading', translations: { ru: 'Сборка и загрузка' }, slug: 'sdk/building-and-loading' },
          ],
        },
      ],
    }),
  ],
});
