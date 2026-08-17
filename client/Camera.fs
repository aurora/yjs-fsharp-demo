/// Screen (browser pixels, origin = canvas top-left) <-> World (shared, zoom/pan
/// independent board coordinates) conversions. Every coordinate that goes out onto
/// the wire (note positions, cursor positions) is in World space - that's what makes
/// "follow" work correctly even when the two users have different window sizes or
/// zoom levels.
module Client.Camera

open Client.Types

let worldToScreen (cam: Camera) (p: Vec2) : Vec2 =
    { X = (p.X - cam.X) * cam.Zoom
      Y = (p.Y - cam.Y) * cam.Zoom }

let screenToWorld (cam: Camera) (p: Vec2) : Vec2 =
    { X = p.X / cam.Zoom + cam.X
      Y = p.Y / cam.Zoom + cam.Y }

let minZoom = 0.25
let maxZoom = 3.0

/// Zooms while keeping the world point currently under `screenPoint` visually stationary
/// (the usual "zoom towards the mouse cursor" behaviour).
let zoomAt (screenPoint: Vec2) (factor: float) (cam: Camera) : Camera =
    let newZoom = max minZoom (min maxZoom (cam.Zoom * factor))
    let worldPoint = screenToWorld cam screenPoint

    { cam with
        Zoom = newZoom
        X = worldPoint.X - screenPoint.X / newZoom
        Y = worldPoint.Y - screenPoint.Y / newZoom }

/// Re-centers the camera on a world point (used by follow-mode), keeping current zoom.
let centerOn (viewportW: float) (viewportH: float) (worldPoint: Vec2) (cam: Camera) : Camera =
    { cam with
        X = worldPoint.X - viewportW / 2.0 / cam.Zoom
        Y = worldPoint.Y - viewportH / 2.0 / cam.Zoom }
