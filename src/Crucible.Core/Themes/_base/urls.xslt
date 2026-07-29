<?xml version="1.0" encoding="UTF-8"?>
<!--
  The single definition of how a manifest @path becomes a URL.

  Imported by _base/elements.xslt (and therefore by every built-in theme's
  page.xslt) and by _base/sitemap.xslt, so nav links, canonical/og:url tags and
  sitemap <loc> entries cannot drift apart. search.js mirrors this rule in
  JavaScript; LinkFormatTests pins the two together.
-->
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                xmlns:c="https://phoenixml.dev/crucible"
                exclude-result-prefixes="xs c"
                version="3.0">

  <!--
    URLs are extensionless, and a trailing "index" segment collapses to its
    directory so that a page and the directory it indexes share one canonical
    URL:

      index                 ->  <base>
      guides/install        ->  <base>guides/install
      guides/index          ->  <base>guides/
  -->
  <xsl:function name="c:page-url" as="xs:string">
    <xsl:param name="base-url" as="xs:string"/>
    <xsl:param name="path" as="xs:string"/>
    <xsl:sequence select="concat($base-url, replace($path, '(^|/)index$', '$1'))"/>
  </xsl:function>

</xsl:stylesheet>
