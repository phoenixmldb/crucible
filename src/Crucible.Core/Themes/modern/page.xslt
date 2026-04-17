<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="3.0">
  <xsl:output method="html" html-version="5" indent="yes" encoding="UTF-8"/>
  <xsl:template match="document">
    <html lang="en"><head><title><xsl:value-of select="@title"/></title></head>
      <body><xsl:apply-templates select="body/*"/></body>
    </html>
  </xsl:template>
</xsl:stylesheet>
