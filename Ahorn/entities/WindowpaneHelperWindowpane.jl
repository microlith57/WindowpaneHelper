module WindowpaneHelperWindowpane

using ..Ahorn, Maple

@mapdef Entity "WindowpaneHelper/Windowpane" Windowpane(
  x::Integer, y::Integer,
  width::Integer=8, height::Integer=8,
  depth::Integer=11000,
  wipeColor::String="000000ff", overlayColor::String="ffffff",
  background::Bool=true, foreground::Bool=false,
  stylegroundTag::String=""
)

const placements = Ahorn.PlacementDict(
    "Windowpane" => Ahorn.EntityPlacement(
      Windowpane, "rectangle"
    )
)

const windowFillColor = (0.6, 0.6, 0.6, 0.4)
const windowBorderColor = (0.4, 0.4, 0.4, 1.0)

Ahorn.nodeLimits(entity::Windowpane) = 0, 1

Ahorn.minimumSize(entity::Windowpane) = 1, 1
Ahorn.resizable(entity::Windowpane) = true, true

function Ahorn.selection(entity::Windowpane)
  x, y = Ahorn.position(entity)

  width = Int(get(entity.data, "width", 8))
  height = Int(get(entity.data, "height", 8))

  nodes = get(entity.data, "nodes", ())
  if isempty(nodes)
      return Ahorn.Rectangle(x, y, width, height)

  else
      nx, ny = Int.(nodes[1])
      return [Ahorn.Rectangle(x, y, width, height), Ahorn.Rectangle(nx, ny, width, height)]
  end
end

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

function Ahorn.renderSelectedAbs(ctx::Ahorn.Cairo.CairoContext, entity::Windowpane)
  x, y = Ahorn.position(entity)
  nodes = get(entity.data, "nodes", ())

  width = Int(get(entity.data, "width", 8))
  height = Int(get(entity.data, "height", 8))

  if !isempty(nodes)
      nx, ny = Int.(nodes[1])

      cox, coy = floor(Int, width / 2), floor(Int, height / 2)

      renderWindow(ctx, nx, ny, width, height)
      Ahorn.drawArrow(ctx, x + cox, y + coy, nx + cox, ny + coy, Ahorn.colors.selection_selected_fc, headLength=6)
  end
end

end