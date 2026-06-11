namespace OxidizePdf.NET.Models;

/// <summary>
/// A single color stop of a gradient shading: a position along the gradient
/// axis and the RGB color at that position.
/// </summary>
/// <param name="Position">Position along the gradient, in <c>[0.0, 1.0]</c>.</param>
/// <param name="Red">Red component, in <c>[0.0, 1.0]</c>.</param>
/// <param name="Green">Green component, in <c>[0.0, 1.0]</c>.</param>
/// <param name="Blue">Blue component, in <c>[0.0, 1.0]</c>.</param>
public readonly record struct GradientStop(double Position, double Red, double Green, double Blue);
