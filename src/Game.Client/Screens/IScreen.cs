using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Screens;

public interface IScreen
{
    void Update(GameTime time);
    void Draw(SpriteBatch spriteBatch);
}
