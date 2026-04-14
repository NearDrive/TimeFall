using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client;

public static class RenderPrimitives
{
    private static Texture2D? _pixel;

    public static Texture2D Pixel => _pixel ?? throw new InvalidOperationException("RenderPrimitives.Initialize must be called before rendering.");

    public static void Initialize(GraphicsDevice graphicsDevice)
    {
        if (_pixel is not null)
        {
            return;
        }

        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
    }
}
