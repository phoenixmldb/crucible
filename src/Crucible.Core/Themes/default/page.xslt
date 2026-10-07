<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:c="https://phoenixml.dev/crucible"
                exclude-result-prefixes="c"
                version="3.0">
  <!-- Shared body-element templates. Must be the first declaration. -->
  <xsl:import href="../_base/elements.xslt"/>

  <xsl:output method="html" html-version="5" indent="yes" encoding="UTF-8"/>

  <xsl:param name="site-manifest-uri" select="''"/>
  <xsl:param name="base-url" select="'/'"/>
  <xsl:param name="site-title" select="'Documentation'"/>
  <xsl:param name="current-path" select="''"/>
  <xsl:param name="ga4-id" select="''"/>
  <xsl:param name="social-image" select="''"/>
  <xsl:param name="social-image-alt" select="''"/>
  <xsl:param name="social-card" select="''"/>
  <xsl:param name="twitter-site" select="''"/>
  <xsl:param name="favicon" select="''"/>

  <xsl:variable name="manifest" select="if ($site-manifest-uri != '') then doc($site-manifest-uri) else ()"/>

  <xsl:template match="document">
    <html lang="en">
      <head>
        <meta charset="UTF-8"/>
        <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
        <title><xsl:value-of select="@title"/> — <xsl:value-of select="$site-title"/></title>
        <xsl:if test="@description">
          <meta name="description" content="{@description}"/>
        </xsl:if>
        <link rel="canonical" href="{c:page-url($base-url, @path)}"/>
        <!-- Open Graph -->
        <meta property="og:title" content="{@title} — {$site-title}"/>
        <meta property="og:type" content="article"/>
        <meta property="og:url" content="{c:page-url($base-url, @path)}"/>
        <xsl:if test="@description">
          <meta property="og:description" content="{@description}"/>
        </xsl:if>
        <meta property="og:site_name" content="{$site-title}"/>
        <!-- Link previews (X/Twitter and others). A relative image or icon is joined to base-url. -->
        <xsl:variable name="social-image-url" select="if ($social-image = '') then '' else if (matches($social-image, '^https?://')) then $social-image else concat($base-url, $social-image)"/>
        <xsl:choose>
          <xsl:when test="$social-image-url != ''">
            <meta property="og:image" content="{$social-image-url}"/>
            <meta name="twitter:card" content="{if ($social-card = 'summary_large_image') then 'summary_large_image' else 'summary'}"/>
            <meta name="twitter:image" content="{$social-image-url}"/>
            <xsl:if test="$social-image-alt != ''">
              <meta property="og:image:alt" content="{$social-image-alt}"/>
              <meta name="twitter:image:alt" content="{$social-image-alt}"/>
            </xsl:if>
          </xsl:when>
          <xsl:otherwise>
            <meta name="twitter:card" content="summary"/>
          </xsl:otherwise>
        </xsl:choose>
        <meta name="twitter:title" content="{@title} — {$site-title}"/>
        <xsl:if test="@description">
          <meta name="twitter:description" content="{@description}"/>
        </xsl:if>
        <xsl:if test="$twitter-site != ''">
          <meta name="twitter:site" content="{$twitter-site}"/>
        </xsl:if>
        <xsl:if test="$favicon != ''">
          <link rel="icon" href="{if (matches($favicon, '^https?://')) then $favicon else concat($base-url, $favicon)}"/>
        </xsl:if>
        <link rel="stylesheet" href="{$base-url}css/style.css"/>
        <script>
          window.CRUCIBLE_BASE = "<xsl:value-of select="$base-url"/>";
          (function(){var t=localStorage.getItem("crucible-theme");if(t)document.documentElement.setAttribute("data-theme",t)})();
        </script>
        <xsl:if test="$ga4-id != ''">
          <script async="async" src="https://www.googletagmanager.com/gtag/js?id={$ga4-id}"/>
          <script>
            window.dataLayer = window.dataLayer || [];
            function gtag(){dataLayer.push(arguments);}
            gtag('js', new Date());
            gtag('config', '<xsl:value-of select="$ga4-id"/>');
          </script>
        </xsl:if>
      </head>
      <body>
        <header class="site-header">
          <div class="header-content">
            <a href="{$base-url}" class="site-logo"><xsl:value-of select="$site-title"/></a>
            <div class="header-actions">
              <button class="theme-toggle" id="theme-toggle" aria-label="Toggle dark mode" title="Toggle dark mode">&#9790;</button>
              <button class="nav-toggle" aria-label="Toggle navigation">&#9776;</button>
            </div>
          </div>
        </header>
        <div class="layout">
          <nav class="sidebar" aria-label="Documentation">
            <div class="search-container">
              <input type="search" id="search-input" placeholder="Search docs..." aria-label="Search documentation"/>
              <div id="search-results" class="search-results" hidden="hidden"></div>
            </div>
            <xsl:if test="$manifest">
              <xsl:apply-templates select="$manifest/site" mode="nav"/>
            </xsl:if>
          </nav>
          <main>
            <article>
              <xsl:apply-templates select="body/*"/>
            </article>
          </main>
        </div>
        <footer class="site-footer">
          <p>Built with <a href="https://github.com/phoenixmldb/crucible" target="_blank" rel="noopener noreferrer">Crucible</a> by <a href="https://endpointsystems.com" target="_blank" rel="noopener noreferrer">Endpoint Systems</a></p>
        </footer>
        <script src="{$base-url}js/lunr.js"></script>
        <script src="{$base-url}js/search.js"></script>
        <script src="{$base-url}js/theme.js"></script>
        <xsl:call-template name="mermaid-scripts"/>
      </body>
    </html>
  </xsl:template>

  <!-- Navigation templates -->
  <xsl:template match="site" mode="nav">
    <ul class="nav-tree">
      <xsl:apply-templates select="page|section" mode="nav"/>
    </ul>
  </xsl:template>

  <xsl:template match="section" mode="nav">
    <xsl:variable name="has-active" select="exists(.//page[@path = $current-path])"/>
    <li class="nav-section{if ($has-active) then ' open' else ''}">
      <button class="nav-section-toggle" aria-expanded="{if ($has-active) then 'true' else 'false'}">
        <span class="nav-chevron">&#9656;</span>
        <xsl:value-of select="@title"/>
      </button>
      <ul class="nav-section-children">
        <xsl:apply-templates select="page|section" mode="nav"/>
      </ul>
    </li>
  </xsl:template>

  <xsl:template match="page" mode="nav">
    <li>
      <xsl:if test="@path = $current-path">
        <xsl:attribute name="class">active</xsl:attribute>
      </xsl:if>
      <a href="{c:page-url($base-url, @path)}"><xsl:value-of select="@title"/></a>
    </li>
  </xsl:template>

</xsl:stylesheet>
