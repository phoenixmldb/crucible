<?xml version="1.0" encoding="UTF-8"?>
<!--
  Shared body-element templates for the built-in themes.

  Imported by default/page.xslt and modern/page.xslt. Because xsl:import gives
  imported templates LOWER precedence, a theme overrides any of these simply by
  declaring a template with the same match pattern in its own page.xslt.

  This file is not a theme and is never copied to site output — ThemeLoader only
  walks each theme's css/ and js/ directories. It is resolved at transform time
  via the base URI of the importing page.xslt, so it must stay a sibling
  directory of the built-in themes.

  Note: custom themes (theme: ./my-theme) live outside Themes/ and therefore
  cannot import this file. They must be self-contained.
-->
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="3.0">

  <xsl:param name="base-url" select="'/'"/>

  <xsl:template match="heading">
    <xsl:element name="h{@level}">
      <xsl:attribute name="id"><xsl:value-of select="@id"/></xsl:attribute>
      <a class="anchor" href="#{@id}">#</a>
      <xsl:apply-templates/>
    </xsl:element>
  </xsl:template>

  <xsl:template match="paragraph">
    <p><xsl:apply-templates/></p>
  </xsl:template>

  <!-- Plain code block. The modern theme overrides this with a <figure>
       wrapper carrying a filename caption and a copy button. -->
  <xsl:template match="code-block">
    <pre><code>
      <xsl:if test="@language">
        <xsl:attribute name="class">language-<xsl:value-of select="@language"/></xsl:attribute>
      </xsl:if>
      <xsl:value-of select="."/>
    </code></pre>
  </xsl:template>

  <xsl:template match="code">
    <code><xsl:value-of select="."/></code>
  </xsl:template>

  <xsl:template match="list[@type='unordered']">
    <ul><xsl:apply-templates select="item"/></ul>
  </xsl:template>

  <xsl:template match="list[@type='ordered']">
    <ol><xsl:apply-templates select="item"/></ol>
  </xsl:template>

  <xsl:template match="item">
    <li><xsl:apply-templates/></li>
  </xsl:template>

  <xsl:template match="link">
    <a href="{@href}">
      <xsl:if test="@title"><xsl:attribute name="title"><xsl:value-of select="@title"/></xsl:attribute></xsl:if>
      <xsl:apply-templates/>
    </a>
  </xsl:template>

  <xsl:template match="image">
    <img src="{@src}" alt="{@alt}">
      <xsl:if test="@title"><xsl:attribute name="title"><xsl:value-of select="@title"/></xsl:attribute></xsl:if>
    </img>
  </xsl:template>

  <xsl:template match="emphasis">
    <em><xsl:apply-templates/></em>
  </xsl:template>

  <xsl:template match="strong">
    <strong><xsl:apply-templates/></strong>
  </xsl:template>

  <xsl:template match="blockquote">
    <blockquote><xsl:apply-templates/></blockquote>
  </xsl:template>

  <!-- The modern theme overrides this to add a horizontal-scroll wrapper. -->
  <xsl:template match="table">
    <table><xsl:apply-templates/></table>
  </xsl:template>

  <xsl:template match="table-head">
    <thead><xsl:apply-templates/></thead>
  </xsl:template>

  <xsl:template match="table-body">
    <tbody><xsl:apply-templates/></tbody>
  </xsl:template>

  <xsl:template match="row">
    <tr><xsl:apply-templates/></tr>
  </xsl:template>

  <xsl:template match="cell[@header='true']">
    <th><xsl:apply-templates/></th>
  </xsl:template>

  <xsl:template match="cell">
    <td><xsl:apply-templates/></td>
  </xsl:template>

  <xsl:template match="thematic-break">
    <hr/>
  </xsl:template>

  <xsl:template match="admonition">
    <div class="admonition admonition-{@type}">
      <p class="admonition-title">
        <xsl:value-of select="upper-case(substring(@type, 1, 1))"/>
        <xsl:value-of select="substring(@type, 2)"/>
      </p>
      <xsl:apply-templates/>
    </div>
  </xsl:template>

  <!-- Emits markup only. The mermaid runtime and its init script are included
       once per page by page.xslt (see the mermaid-scripts template), not once
       per diagram. -->
  <xsl:template match="mermaid">
    <div class="mermaid-wrapper">
      <pre class="mermaid"><xsl:value-of select="."/></pre>
    </div>
  </xsl:template>

  <!-- Called by each theme at the end of <body>, guarded on the page actually
       containing a diagram, so pages without one pay nothing. -->
  <xsl:template name="mermaid-scripts">
    <xsl:if test="body//mermaid">
      <script src="https://cdn.jsdelivr.net/npm/mermaid@11.6.0/dist/mermaid.min.js"
              integrity="sha384-zkWMJO4sgpPUzyuOgDx8HB/K55glbAwajEpk1Go2NWRuPkPA/wIhoEJTuSkmOYrV"
              crossorigin="anonymous"></script>
      <script src="{$base-url}js/mermaid-init.js"></script>
    </xsl:if>
  </xsl:template>

</xsl:stylesheet>
