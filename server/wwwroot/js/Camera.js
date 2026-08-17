
import { Camera, Vec2 } from "./Types.js";
import { min, max } from "./fable_modules/fable-library-js.5.13.0/Double.js";

export function worldToScreen(cam, p) {
    return new Vec2((p.X - cam.X) * cam.Zoom, (p.Y - cam.Y) * cam.Zoom);
}

export function screenToWorld(cam, p) {
    return new Vec2((p.X / cam.Zoom) + cam.X, (p.Y / cam.Zoom) + cam.Y);
}

export const minZoom = 0.25;

export const maxZoom = 3;

/**
 * Zooms while keeping the world point currently under `screenPoint` visually stationary
 * (the usual "zoom towards the mouse cursor" behaviour).
 */
export function zoomAt(screenPoint, factor, cam) {
    const newZoom = max(minZoom, min(maxZoom, cam.Zoom * factor));
    const worldPoint = screenToWorld(cam, screenPoint);
    return new Camera(worldPoint.X - (screenPoint.X / newZoom), worldPoint.Y - (screenPoint.Y / newZoom), newZoom);
}

/**
 * Re-centers the camera on a world point (used by follow-mode), keeping current zoom.
 */
export function centerOn(viewportW, viewportH, worldPoint, cam) {
    return new Camera(worldPoint.X - ((viewportW / 2) / cam.Zoom), worldPoint.Y - ((viewportH / 2) / cam.Zoom), cam.Zoom);
}

