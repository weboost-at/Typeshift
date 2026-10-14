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
          items: [{ text: 'Getting started', link: '/guide/getting-started' }]
        },
        {
          text: 'Concepts',
          items: [
            { text: 'Why code-defined', link: '/guide/why-code-defined' },
            { text: 'Settings layering', link: '/guide/settings-layering' }
          ]
        },
        {
          text: 'Building agents',
          items: [
            { text: 'Toolsets', link: '/guide/toolsets' },
            { text: 'Tool settings', link: '/guide/tool-settings' },
            { text: 'Agents', link: '/guide/defining-agents' },
            { text: 'Running agents', link: '/guide/running-agents' },
            { text: 'Settings and stores', link: '/guide/settings-and-stores' },
            { text: 'Conversations', link: '/guide/conversations' },
            { text: 'Tool approval', link: '/guide/tool-approval' },
            { text: 'Plan and auto modes', link: '/guide/modes' },
            { text: 'Middleware', link: '/guide/middleware' },
            { text: 'Context providers', link: '/guide/context-providers' },
            { text: 'MCP servers', link: '/guide/mcp' },
            { text: 'Multiple agents', link: '/guide/multi-agent' },
            { text: 'Long-running agents', link: '/guide/long-running-agents' },
            { text: 'Observability', link: '/guide/observability' },
            { text: 'Built-in toolsets', link: '/guide/built-in-toolsets' },
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
            { text: 'Releasing', link: '/contributing/releasing' }
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
