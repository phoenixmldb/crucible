<?xml version="1.0" encoding="UTF-8"?>
<!--
  Shared sitemap.xml generator. Imported by each built-in theme's sitemap.xslt.

  ThemeLoader requires every theme to expose its own sitemap.xslt, so themes
  keep a thin file that imports this one and can override a template if they
  ever need to.
-->
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="3.0">
  <xsl:output method="xml" indent="yes" encoding="UTF-8"/>

  <xsl:template match="site">
    <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
      <xsl:apply-templates select=".//page"/>
    </urlset>
  </xsl:template>

  <xsl:template match="page">
    <url xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
      <loc><xsl:value-of select="concat(ancestor::site/@base-url, @path, '.html')"/></loc>
      <xsl:if test="@updated">
        <lastmod><xsl:value-of select="@updated"/></lastmod>
      </xsl:if>
    </url>
  </xsl:template>
</xsl:stylesheet>
