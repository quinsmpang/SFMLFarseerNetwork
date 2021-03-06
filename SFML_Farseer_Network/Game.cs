﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using FarseerPhysics.Factories;
using FarseerPhysics.Dynamics;
using SFML.Graphics;
using SFML.Window;
using SFML_Farseer_Network.Managers;

namespace SFML_Farseer_Network
{
    using Key = Keyboard.Key;
    using Vector2 = Microsoft.Xna.Framework.Vector2;

    public enum GameState
    {
        Setup,
        Waiting,
        Ready
    }

    public class Game : IDisposable
    {
        public const int MAX_MESSAGES = 20;
        private RenderWindow _window;
        private Font _font;
        private Text _title;
        private List<Text> _messages;
        private GameState _state;
        private List<Text> _setupOptions;
        private KeyboardState _newKeyState;
        private KeyboardState _oldKeyState;
        private MouseState _newMouseState;
        private MouseState _oldMouseState;
        private NetManager _netManager;
        private EntityManager _entityManager;
        private PhysicsManager _physicsManager;
        private CameraManager _cameraManager;
        private Text _ipPrompt;
        private Text _ipAddressText;
        private string _ipAddressValue;
        private Stopwatch _stopwatch;
        private float _targetDt = 1f / 60f;
        private int _fps = 0;
        private Text _fpsText;
        private bool _inFocus = true;
        private Vector2f _mouseWorld;
        private CircleShape _mousePointer;

        public PhysicsManager physicsManager { get { return _physicsManager; } }
        public EntityManager entityManager { get { return _entityManager; } }
        public NetManager netManager { get { return _netManager; } }
        public RenderWindow window { get { return _window; } }
        public GameState state { get { return _state; } }
        public bool inFocus { get { return _inFocus; } }
        public KeyboardState newKeyState { get { return _newKeyState; } }
        public KeyboardState oldKeyState { get { return _oldKeyState; } }
        public MouseState newMouseState { get { return _newMouseState; } }
        public MouseState oldMouseState { get { return _oldMouseState; } }
        public Vector2f mouseWorld { get { return _mouseWorld; } }

        public Game()
        {
            _state = GameState.Setup;
            _window = new RenderWindow(new VideoMode(800, 600), "Farseer Network Test");
            _window.Closed += new EventHandler(_window_Closed);
            _window.GainedFocus += new EventHandler(_window_GainedFocus);
            _window.LostFocus += new EventHandler(_window_LostFocus);
            _netManager = new NetManager(this);
            _ipAddressValue = "127.0.0.1";
            _stopwatch = new Stopwatch();

            loadContent();
        }

        void _window_LostFocus(object sender, EventArgs e)
        {
            _inFocus = false;
        }

        void _window_GainedFocus(object sender, EventArgs e)
        {
            _inFocus = true;
        }

        void _window_Closed(object sender, EventArgs e)
        {
            _window.Close();
        }

        public void Dispose()
        {
        }

        private void loadContent()
        {
            _font = new Font(@"resources\courbd.ttf");
            _messages = new List<Text>();
            _title = new Text("SFML_Farseer_Network", _font, 18);
            _title.Color = Color.White;
            _title.Position = new Vector2f(16, 16);

            Text clientOption = new Text("1. Start Client", _font, 14);
            clientOption.Position = new Vector2f(340, 300);
            clientOption.Color = Color.Red;

            Text serverOption = new Text("2. Start Server", _font, 14);
            serverOption.Position = new Vector2f(340, 332);
            serverOption.Color = Color.Red;

            _setupOptions = new List<Text>();
            _setupOptions.Add(clientOption);
            _setupOptions.Add(serverOption);

            _ipPrompt = new Text("Please enter an IP address:", _font, 14);
            _ipPrompt.Position = new Vector2f(340, 300);
            _ipPrompt.Color = Color.Red;

            _ipAddressText = new Text("", _font, 18);
            _ipAddressText.Position = new Vector2f(340, 332);
            _ipAddressText.Color = Color.Green;

            _fpsText = new Text("FPS: 0", _font, 18);
            _fpsText.Color = Color.Red;
            _fpsText.Position = new Vector2f(700, 16);

            _mousePointer = new CircleShape(0.1f);
            _mousePointer.Origin = new Vector2f(0.05f, 0.05f);
            _mousePointer.FillColor = Color.Yellow;
            _mousePointer.Position = new Vector2f(-100, -100);
        }

        public void run()
        {
            float updateTime = 0f;
            float frameTime = 0f;
            bool drawn = false;
            int frameCount = 0;

            _stopwatch.Start();
            while (_window.IsOpen())
            {
                float currentTime = (float)_stopwatch.Elapsed.TotalSeconds;

                updateTime += currentTime;
                frameTime += currentTime;
                _stopwatch.Restart();

                while (updateTime >= _targetDt)
                {
                    update();
                    updateTime -= _targetDt;
                    drawn = false;
                    frameCount++;
                }

                if (drawn)
                {
                    Thread.Sleep(1);
                }
                else
                {
                    draw();
                    drawn = true;
                }

                if (frameTime >= 1)
                {
                    _fps = frameCount;
                    frameCount = 0;
                    frameTime = 0;
                }
            }
        }

        public void addMessage(string str)
        {
            Text text = new Text(str, _font, 14);

            text.Color = Color.Yellow;

            if (_messages.Count >= MAX_MESSAGES)
            {
                _messages.RemoveAt(0);
            }

            _messages.Add(text);
        }

        public void startGame()
        {
            addMessage("Starting game...");
            _state = GameState.Ready;
            _entityManager = new EntityManager(this);
            _cameraManager = new CameraManager(this);
            _physicsManager = new PhysicsManager(this);
        }

        public void update()
        {
            _window.DispatchEvents();
            _oldKeyState = _newKeyState;
            _newKeyState = KeyboardState.get();
            _oldMouseState = _newMouseState;
            _newMouseState = MouseState.get();

            if (_state == GameState.Setup)
            {
                if (_inFocus)
                {
                    if (_newKeyState.isPressed(Key.Num1) && _oldKeyState.isReleased(Key.Num1))
                    {
                        _netManager.startClient();
                        _state = GameState.Waiting;
                    }
                    else if (_newKeyState.isPressed(Key.Num2) && _oldKeyState.isReleased(Key.Num2))
                    {
                        _netManager.startServer();
                        _state = GameState.Waiting;
                    }
                }
            }
            else if (_state == GameState.Waiting)
            {
                if (_netManager.role == NetRole.Client)
                {
                    if (_inFocus && !_netManager.connected)
                    {
                        // 0-9
                        for (int i = 0; i < 10; i++)
                        {
                            Key key = (Key)(i + 26);
                            if (_newKeyState.isPressed(key) && _oldKeyState.isReleased(key))
                            {
                                _ipAddressValue += i.ToString();
                            }
                        }

                        // .
                        if (_newKeyState.isPressed(Key.Period) && _oldKeyState.isReleased(Key.Period))
                        {
                            _ipAddressValue += ".";
                        }

                        // Backspace
                        if (_newKeyState.isPressed(Key.Back) && _oldKeyState.isReleased(Key.Back))
                        {
                            if (_ipAddressValue.Length > 0)
                            {
                                _ipAddressValue = _ipAddressValue.Substring(0, _ipAddressValue.Length - 1);
                            }
                        }

                        // Enter
                        if (_newKeyState.isPressed(Key.Return) && _oldKeyState.isReleased(Key.Return))
                        {
                            _netManager.connectTo(_ipAddressValue);
                            addMessage("Attempting to connect to " + _ipAddressValue + ":3456...");
                        }

                        _ipAddressText.DisplayedString = _ipAddressValue;
                    }
                    else
                    {
                        startGame();
                    }
                }
                else    // NetRole.Server
                {
                    if (_netManager.connected)
                    {
                        startGame();
                    }
                }

                _netManager.update();
            }
            else if (_state == GameState.Ready)
            {
                if (_inFocus)
                {
                    // Mouse position
                    _mouseWorld = _window.MapPixelToCoords(_window.InternalGetMousePosition(), _cameraManager.worldView);
                }

                // Manager updates
                _netManager.update();
                _physicsManager.update();
            }

            // FPS
            _fpsText.DisplayedString = "FPS: " + _fps.ToString();
        }

        public void draw()
        {
            _window.Clear(Color.Black);

            if (_state == GameState.Setup)
            {
                for (int i = 0; i < _setupOptions.Count; i++)
                {
                    _window.Draw(_setupOptions[i]);
                }
            }
            else if (_state == GameState.Waiting)
            {
                if (_netManager.role == NetRole.Client)
                {
                    _window.Draw(_ipPrompt);
                    _window.Draw(_ipAddressText);
                }
            }
            else if (_state == GameState.Ready)
            {
                // Switch to world view
                _window.SetView(_cameraManager.worldView);

                // Box2d debug view
                _physicsManager.drawDebugView();

                // Mouse pointer
                _mousePointer.Position = _mouseWorld;
                _window.Draw(_mousePointer);

                // Restore default view
                _window.SetView(_window.DefaultView);
            }

            // Draw title
            _window.Draw(_title);

            // Draw messages
            for (int i = _messages.Count - 1; i >= 0; i--)
            {
                Vector2f position = new Vector2f(16, (i + 1) * 16 + 32);
                Text message = _messages[i];

                message.Position = position;
                _window.Draw(message);
            }

            // Draw FPS
            _window.Draw(_fpsText);

            _window.Display();
        }
    }
}
