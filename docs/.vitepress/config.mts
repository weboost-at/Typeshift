import { defineConfig } from 'vitepress'

const repository = 'https://github.com/weboost-at/Typeshift'

export default defineConfig({
  title: 'Typeshift',
  description: 'Code-defined agents and toolsets for .NET on Microsoft.Extensions.AI',
  // GitHub Pages serves a project site under the repository's name; the Pages workflow passes the real base
  base: process.env.DOCS_BASE ?? '/Typeshift/',
  cleanUrls: true,
  lastUpdated: true,
  markdown: {
    // mdsnippets names a fence after the file's extension
    languageAlias: { csproj: 'xml' }
  },
  themeConfig: {
    nav: [
      { text: 'Guide', link: '/guide/getting-started', activeMatch: '/guide/' },
      { text: 'Diagnostics', link: '/guide/diagnostics' },
      { text: 'Contributing', link: '/contributing/', activeMatch: '/contributing/' },
      { text: 'Changelog', link: `${repository}/blob/main/CHANGELOG.md` }
    ],
    sidebar: {
      '/guide/': [
        {
          text: 'Introduction',
          items: [
            { text: 'Getting started', link: '/guide/getting-started' }
          ]
        },
        {
          text: 'Building agents',
          items: [
            { text: 'Toolsets', link: '/guide/toolsets' },
            { text: 'Tool settings', link: '/guide/tool-settings' },
            { text: 'Agents', link: '/guide/defining-agents' },
            { text: 'Running agents', link: '/guide/running-agents' }
          ]
        },
        {
          text: 'Persistence',
          items: [
            { text: 'Stores and settings', link: '/guide/persistence' },
            { text: 'Conversations', link: '/guide/conversations' }
          ]
        },
        {
          text: 'Reference',
          items: [
            { text: 'Native AOT', link: '/guide/native-aot' },
            { text: 'Diagnostics', link: '/guide/diagnostics' }
          ]
        }
      ],
      '/contributing/': [
        {
          text: 'Contributing',
          items: [
            { text: 'Overview', link: '/contributing/' },
            { text: 'Architecture', link: '/contributing/architecture' },
            { text: 'Decisions', link: '/contributing/decisions' },
            { text: 'Status', link: '/contributing/status' },
            { text: 'Documentation', link: '/contributing/documentation' },
            { text: 'Releasing', link: '/contributing/releasing' },
            { text: 'Naming', link: '/contributing/naming' },
            { text: 'Renaming', link: '/contributing/renaming' }
          ]
        }
      ]
    },
    socialLinks: [{ icon: 'github', link: repository }],
    editLink: {
      pattern: `${repository}/edit/main/docs/:path`,
      text: 'Edit this page on GitHub'
    },
    search: { provider: 'local' },
    outline: [2, 3],
    footer: {
      message: 'Released under the MIT License.'
    }
  }
})
