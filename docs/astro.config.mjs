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
          items: [
            { label: 'Overview', slug: 'getting-started/overview' },
            { label: 'Install & run', slug: 'getting-started/install' },
          ],
        },
        {
          label: 'Guides',
          items: [
            { label: 'Loading logs', slug: 'guides/loading-logs' },
            { label: 'Exploring & filtering', slug: 'guides/exploring' },
            { label: 'Local event store', slug: 'guides/event-store' },
            { label: 'Reports', slug: 'guides/reports' },
            { label: 'Plugins', slug: 'guides/plugins' },
          ],
        },
        {
          label: 'SDK',
          items: [
            { label: 'Overview', slug: 'sdk/overview' },
            { label: 'Sort plugins', slug: 'sdk/sort-plugins' },
            { label: 'Filter plugins', slug: 'sdk/filter-plugins' },
            { label: 'Event model', slug: 'sdk/event-model' },
            { label: 'Building & loading', slug: 'sdk/building-and-loading' },
          ],
        },
      ],
    }),
  ],
});
