using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Game.Client;

public sealed class InputHandler
{
    private MouseState _currentMouse;
    private MouseState _previousMouse;
    private KeyboardState _currentKeyboard;
    private KeyboardState _previousKeyboard;

    public Point MousePosition => _currentMouse.Position;

    public void Update()
    {
        _previousMouse = _currentMouse;
        _previousKeyboard = _currentKeyboard;

        _currentMouse = Mouse.GetState();
        _currentKeyboard = Keyboard.GetState();
    }

    public bool IsKeyPressed(Keys key)
    {
        return _currentKeyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    public bool IsLeftClick(Rectangle region)
    {
        return _currentMouse.LeftButton == ButtonState.Pressed
            && _previousMouse.LeftButton == ButtonState.Released
            && region.Contains(_currentMouse.Position);
    }
}
