module WindowpaneHelperWindowpane

using ..Ahorn, Maple

@mapdef Entity "WindowpaneHelper/Windowpane" Windowpane(
  x::Integer, y::Integer,
  width::Integer=8, height::Integer=8,
  depth::Integer=11000,
  wipeColor::String="000000ff", overlayColor::String="ffffff",
  stylegroundTag::String=""
)

const placements = Ahorn.PlacementDict(
    "Windowpane" => Ahorn.EntityPlacement(
      Windowpane, "rectangle"
    )
)

const windowFillColor = (0.6, 0.6, 0.6, 0.4)
const windowBorderColor = (0.4, 0.4, 0.4, 1.0)

Ahorn.minimumSize(entity::Windowpane) = 1, 1
Ahorn.resizable(entity::Windowpane) = true, true

Ahorn.selection(entity::Windowpane) = Ahorn.getEntityRectangle(entity)

function renderWindow(ctx::Ahorn.Cairo.CairoContext, x::Number, y::Number, width::Number, height::Number)
    Ahorn.Cairo.save(ctx)

    Ahorn.set_antialias(ctx, 1)
    Ahorn.set_line_width(ctx, 1)

    Ahorn.drawRectangle(ctx, x, y, width, height, windowFillColor, windowBorderColor)

    Ahorn.restore(ctx)
end

function Ahorn.render(ctx::Ahorn.Cairo.CairoContext, entity::Windowpane, room::Maple.Room)
    x = Int(get(entity.data, "x", 0))
    y = Int(get(entity.data, "y", 0))

    width = Int(get(entity.data, "width", 8))
    height = Int(get(entity.data, "height", 8))

    renderWindow(ctx, 0, 0, width, height)
end

end