<?xml version="1.0" encoding="UTF-8"?>
<!--
  Shared sitemap.xml generator. Imported by each built-in theme's sitemap.xslt.

  ThemeLoader requires every theme to expose its own sitemap.xslt, so themes
  keep a thin file that imports this one and can override a template if they
  ever need to.
-->
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:c="https://phoenixml.dev/crucible"
                exclude-result-prefixes="c"
                version="3.0">

  <xsl:import href="urls.xslt"/>
  <xsl:output method="xml" indent="yes" encoding="UTF-8"/>

  <xsl:template match="site">
    <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
      <!-- Section landing pages (a subdirectory index.md) are rendered as real
           pages but are not manifest <page> nodes, so list them explicitly. -->
      <xsl:apply-templates select=".//page | .//section[@index-path]"/>
    </urlset>
  </xsl:template>

  <xsl:template match="page">
    <url xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
      <loc><xsl:value-of select="c:page-url(ancestor::site/@base-url, @path)"/></loc>
      <xsl:if test="@updated">
        <lastmod><xsl:value-of select="@updated"/></lastmod>
      </xsl:if>
    </url>
  </xsl:template>

  <xsl:template match="section[@index-path]">
    <url xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
      <loc><xsl:value-of select="c:page-url(ancestor::site/@base-url, @index-path)"/></loc>
    </url>
  </xsl:template>
</xsl:stylesheet>
