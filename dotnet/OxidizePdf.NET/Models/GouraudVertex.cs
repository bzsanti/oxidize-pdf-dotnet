namespace OxidizePdf.NET.Models;

/// <summary>A vertex in a Type 4 free-form Gouraud triangle mesh.</summary>
/// <param name="Flag">Edge flag: 0 starts a triangle; 1 or 2 shares an edge.</param>
/// <param name="X">X coordinate in shading space.</param>
/// <param name="Y">Y coordinate in shading space.</param>
/// <param name="Red">Red component in [0,1].</param>
/// <param name="Green">Green component in [0,1].</param>
/// <param name="Blue">Blue component in [0,1].</param>
public readonly record struct GouraudVertex(
    byte Flag, double X, double Y, double Red, double Green, double Blue);
