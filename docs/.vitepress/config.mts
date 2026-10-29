import { defineConfig } from 'vitepress'
import { withMermaid } from 'vitepress-plugin-mermaid'

const repository = 'https://github.com/weboost-at/Typeshift'

// GitHub Pages serves a project site under the repository's name; the Pages workflow passes the real base
const base = process.env.DOCS_BASE ?? '/Typeshift/'

// The sidebar order is the reading order: the Introduction first, which takes a reader from nothing to an app that
// uses the library, then building, running and controlling agents, then the reference. The Introduction has no
// `collapsed` key, so it is always open; the others start collapsed, and VitePress opens one when it holds the current
// page.
// docs/.vitepress/lint.mjs fails on a page the sidebar leaves out.
const sidebar = {
  '/guide/': [
    {
      text: 'Introduction',
      items: [
        { text: 'Getting started', link: '/guide/getting-started' },
        { text: 'Implementing a store', link: '/guide/implementing-a-store' }
      ]
    },
    {
      text: 'Building agents',
      collapsed: true,
      items: [
        { text: 'Toolsets', link: '/guide/toolsets' },
        { text: 'Tool settings', link: '/guide/tool-settings' },
        { text: 'Agents', link: '/guide/defining-agents' },
        { text: 'Built-in toolsets', link: '/guide/built-in-toolsets' },
        { text: 'MCP servers', link: '/guide/mcp' },
        { text: 'Multiple agents', link: '/guide/multi-agent' }
      ]
    },
    {
      text: 'Running agents',
      collapsed: true,
      items: [
        { text: 'Running agents', link: '/guide/running-agents' },
        { text: 'Models and providers', link: '/guide/models' },
        { text: 'Settings and stores', link: '/guide/settings-and-stores' },
        { text: 'Conversations', link: '/guide/conversations' },
        { text: 'In production', link: '/guide/production' }
      ]
    },
    {
      text: 'Controlling runs',
      collapsed: true,
      items: [
        { text: 'Approval and modes', link: '/guide/tool-approval' },
        { text: 'Middleware and context', link: '/guide/middleware' }
      ]
    },
    {
      text: 'Reference',
      collapsed: true,
      items: [
        { text: 'Registration', link: '/guide/registration' },
        { text: 'Builders', link: '/guide/builders' },
        { text: 'Generated members', link: '/guide/generated-members' },
        { text: 'Diagnostics', link: '/guide/diagnostics' },
        { text: 'Glossary', link: '/guide/glossary' }
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
        { text: 'Releasing', link: '/contributing/releasing' },
        { text: 'Writing the docs', link: '/contributing/writing' }
      ]
    }
  ]
}

export default withMermaid(
  defineConfig({
    title: 'Typeshift',
    description: 'Code-defined agents and toolsets for .NET on Microsoft.Extensions.AI',
    base,
    // VitePress leaves head links alone, so they carry the base themselves
    head: [
      ['link', { rel: 'icon', type: 'image/svg+xml', href: `${base}brand/favicon.svg` }],
      ['link', { rel: 'icon', type: 'image/png', sizes: '32x32', href: `${base}brand/favicon-32.png` }],
      ['link', { rel: 'apple-touch-icon', href: `${base}brand/apple-touch-icon.png` }]
    ],
    cleanUrls: true,
    lastUpdated: true,
    // The page templates are shown on the writing page, never served as pages
    srcExclude: ['**/templates/**'],
    markdown: {
      // mdsnippets names a fence after the file's extension
      languageAlias: { csproj: 'xml' }
    },
    themeConfig: {
      logo: { light: '/brand/mark.svg', dark: '/brand/mark-dark.svg', alt: 'Typeshift' },
      nav: [
        { text: 'Guide', link: '/guide/getting-started', activeMatch: '/guide/' },
        { text: 'Diagnostics', link: '/guide/diagnostics' },
        { text: 'Contributing', link: '/contributing/', activeMatch: '/contributing/' },
        { text: 'Changelog', link: `${repository}/blob/main/CHANGELOG.md` }
      ],
      sidebar,
      socialLinks: [{ icon: 'github', link: repository }],
      editLink: {
        pattern: `${repository}/edit/main/docs/:path`,
        text: 'Edit this page on GitHub'
      },
      search: { provider: 'local' },
      outline: { level: [2, 3], label: 'On this page' },
      footer: {
        message: 'Released under the MIT License.'
      }
    },
    mermaid: {
      theme: 'neutral',
      look: 'handDrawn'
    },
    // Mermaid alone is a chunk of several megabytes, loaded only by pages with a diagram
    vite: {
      build: { chunkSizeWarningLimit: 4000 }
    }
  })
)
