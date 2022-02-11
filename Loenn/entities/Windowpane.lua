local windowpane = {}

windowpane.name = "WindowpaneHelper/Windowpane"
windowpane.fillColor = {0.6, 0.6, 0.6, 0.4}
windowpane.borderColor = {0.4, 0.4, 0.4, 1.0}
windowpane.nodeLimits = {0, 1}
windowpane.nodeLineRenderType = "line"
windowpane.placements = {
  name = "windowpane",
  data = {
    width = 8,
    height = 8,
    depth = 11000,
    wipeColor = "000000ff",
    overlayColor = "ffffffff",
    blendState = "alphablend",
    renderPosition = "inLevel",
    punchThrough = false,
    stylegroundTag = ""
  }
}
windowpane.fieldInformation = {
  blendState = {
    options = {"additive", "alphablend", "nonpremultiplied", "opaque"},
    fieldType = "string"
  },
  renderPosition = {
    options = {"inLevel", "above", "below"},
    fieldType = "string"
  }
}

return windowpane
