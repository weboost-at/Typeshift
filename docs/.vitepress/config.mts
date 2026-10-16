import { defineConfig } from 'vitepress'
import { withMermaid } from 'vitepress-plugin-mermaid'

const repository = 'https://github.com/weboost-at/Typeshift'

// The sidebar order is the reading order: the big picture first, then building, running and controlling agents,
// then the reference. docs/.vitepress/lint.mjs fails on a page the sidebar leaves out.
const sidebar = {
  '/guide/': [
    {
      text: 'Introduction',
      items: [
        { text: 'Getting started', link: '/guide/getting-started' },
        { text: 'Comparison', link: '/guide/comparison' }
      ]
    },
    {
      text: 'Building agents',
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
      items: [
        { text: 'Running agents', link: '/guide/running-agents' },
        { text: 'Settings and stores', link: '/guide/settings-and-stores' },
        { text: 'Conversations', link: '/guide/conversations' },
        { text: 'Long-running agents', link: '/guide/long-running-agents' }
      ]
    },
    {
      text: 'Controlling runs',
      items: [
        { text: 'Tool approval', link: '/guide/tool-approval' },
        { text: 'Plan and auto modes', link: '/guide/modes' },
        { text: 'Middleware', link: '/guide/middleware' },
        { text: 'Context providers', link: '/guide/context-providers' }
      ]
    },
    {
      text: 'In production',
      items: [
        { text: 'Observability', link: '/guide/observability' },
        { text: 'Native AOT', link: '/guide/native-aot' }
      ]
    },
    {
      text: 'Reference',
      items: [
        { text: 'Registration', link: '/guide/registration' },
        { text: 'Builders', link: '/guide/builders' },
        { text: 'Generated members', link: '/guide/generated-members' },
        { text: 'Agent host', link: '/guide/agent-host' },
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
    // GitHub Pages serves a project site under the repository's name; the Pages workflow passes the real base
    base: process.env.DOCS_BASE ?? '/Typeshift/',
    cleanUrls: true,
    lastUpdated: true,
    // The page templates are shown on the writing page, never served as pages
    srcExclude: ['**/templates/**'],
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
