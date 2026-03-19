<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="3.0">
  <xsl:output method="html" html-version="5" indent="yes" encoding="UTF-8"/>

  <xsl:param name="base-url" select="'/'"/>
  <xsl:param name="current-path" select="''"/>

  <!-- Standalone navigation fragment generator.
       Input: a <site> element (the site manifest).
       Output: a <nav> HTML fragment suitable for inclusion in a page. -->

  <xsl:template match="site">
    <nav class="sidebar" aria-label="Documentation">
      <ul class="nav-tree">
        <xsl:apply-templates select="page|section"/>
      </ul>
    </nav>
  </xsl:template>

  <xsl:template match="section">
    <li class="nav-section">
      <span class="nav-section-title"><xsl:value-of select="@title"/></span>
      <ul>
        <xsl:apply-templates select="page|section"/>
      </ul>
    </li>
  </xsl:template>

  <xsl:template match="page">
    <li>
      <xsl:if test="@path = $current-path">
        <xsl:attribute name="class">active</xsl:attribute>
      </xsl:if>
      <a href="{$base-url}{@path}.html"><xsl:value-of select="@title"/></a>
    </li>
  </xsl:template>

</xsl:stylesheet>
