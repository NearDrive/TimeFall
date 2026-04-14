using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client;

public static class RenderPrimitives
{
    private static Texture2D? _pixel;
    private static SpriteFont? _font;

    public static Texture2D Pixel => _pixel ?? throw new InvalidOperationException("RenderPrimitives.Initialize must be called before rendering.");
    public static SpriteFont Font => _font ?? throw new InvalidOperationException("RenderPrimitives.Initialize must be called before rendering.");

    public static void Initialize(GraphicsDevice graphicsDevice, ContentManager content)
    {
        if (_pixel is null)
        {
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData([Color.White]);
        }

        _font ??= content.Load<SpriteFont>("DefaultFont");
    }
}
