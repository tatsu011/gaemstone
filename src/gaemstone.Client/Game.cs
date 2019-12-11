using System;
using System.Drawing;
using System.IO;
using System.Numerics;
using gaemstone.Client.Bloxel.Blocks;
using gaemstone.Client.Bloxel.Chunks;
using gaemstone.Client.Components;
using gaemstone.Client.Graphics;
using gaemstone.Common.Bloxel.Chunks;
using gaemstone.Common.ECS;
using gaemstone.Common.ECS.Stores;
using gaemstone.Common.Utility;
using Silk.NET.OpenGL;
using Silk.NET.Windowing.Common;

namespace gaemstone.Client
{
	public class Game
	{
		private static void Main(string[] args)
			=> new Game(args).Run();


		public IWindow Window { get; }
		public Random RND { get; } = new Random();

		public MeshManager MeshManager { get; }
		public ChunkMeshGenerator ChunkMeshGenerator { get; }

		public EntityManager Entities { get; }
		public ComponentManager Components { get; }
		public IComponentRefStore<Transform> Transforms { get; }
		public IComponentRefStore<Mesh> Meshes { get; }
		public IComponentRefStore<Camera> Cameras { get; }
		// TODO: Camera is uncommon. Handle uncommon components differently.

		public Entity MainCamera { get; private set; }


		private Game(string[] args)
		{
			Window = Silk.NET.Windowing.Window.Create(new WindowOptions {
				Title = "gæmstone",
				Size  = new Size(1280, 720),
				API   = GraphicsAPI.Default,
				UpdatesPerSecond = 30.0,
				FramesPerSecond  = 60.0,
			});
			Window.Load    += OnLoad;
			Window.Resize  += OnResize;
			Window.Update  += OnUpdate;
			Window.Render  += OnRender;
			Window.Closing += OnClosing;

			MeshManager        = new MeshManager(this);
			ChunkMeshGenerator = new ChunkMeshGenerator(MeshManager);

			Entities   = new EntityManager();
			Components = new ComponentManager(Entities);
			Transforms = new PackedArrayStore<Transform>();
			Meshes     = new PackedArrayStore<Mesh>();
			Cameras    = new PackedArrayStore<Camera>();
			Components.AddStore(Transforms);
			Components.AddStore(Meshes);
			Components.AddStore(Cameras);
		}

		public void Run()
		{
			Window.Run();
		}


		public Stream GetResourceStream(string name)
			=> typeof(Game).Assembly.GetManifestResourceStream("gaemstone.Client.Resources." + name)!;

		public string GetResourceAsString(string name)
		{
			using (var stream = GetResourceStream(name))
			using (var reader = new StreamReader(stream))
				return reader.ReadToEnd();
		}


		private Program _program;
		private UniformMatrix4x4 _mvpUniform;

		private void OnLoad()
		{
			GFX.Initialize();
			GFX.OnDebugOutput += (source, type, id, severity, message) =>
				Console.WriteLine($"[GLDebug] [{severity}] {type}/{id}: {message}");

			var vertexShaderSource   = GetResourceAsString("default.vs.glsl");
			var fragmentShaderSource = GetResourceAsString("default.fs.glsl");

			_program = Program.LinkFromShaders("main",
				Shader.CompileFromSource("vertex", ShaderType.VertexShader, vertexShaderSource),
				Shader.CompileFromSource("fragment", ShaderType.FragmentShader, fragmentShaderSource));
			_program.DetachAndDeleteShaders();

			var uniforms = _program.GetActiveUniforms();
			var attribs  = _program.GetActiveAttributes();
			_mvpUniform  = uniforms["modelViewProjection"].Matrix4x4;
			MeshManager.ProgramAttributes = attribs;

			var heartMesh = MeshManager.Load("heart.glb").ID;
			var swordMesh = MeshManager.Load("sword.glb").ID;

			MainCamera = Entities.New();
			Transforms.Set(MainCamera.ID, Matrix4x4.CreateLookAt(
				cameraPosition : new Vector3(3, 2, 2),
				cameraTarget   : new Vector3(0, 0, 0),
				cameraUpVector : new Vector3(0, 1, 0)));

			for (var x = -6; x <= 6; x++)
			for (var z = -6; z <= 6; z++) {
				var entity   = Entities.New();
				var position = Matrix4x4.CreateTranslation(x, 0, z);
				var rotation = Matrix4x4.CreateRotationY(RND.NextFloat(MathF.PI * 2));
				Transforms.Set(entity.ID, rotation * position);
				Meshes.Set(entity.ID, RND.Pick(heartMesh, swordMesh));
			}

			var block   = new Block(Entities.New());
			var storage = new ChunkPaletteStorage<Block>(default(Block));
			for (var x = 0; x < 16; x++)
			for (var y = 0; y < 16; y++)
			for (var z = 0; z < 16; z++)
				if (RND.NextBool(0.1))
					storage[x, y, z] = block;

			var chunkMesh = ChunkMeshGenerator.Generate(storage)!;
			var chunk = Entities.New();
			Transforms.Set(chunk.ID, Matrix4x4.CreateScale(0.15F));
			Meshes.Set(chunk.ID, chunkMesh.ID);

			OnResize(Window.Size);
		}

		private void OnClosing()
		{

		}

		private void OnResize(Size size)
		{
			const float DEGREES_TO_RADIANS = MathF.PI / 180;
			var aspectRatio = (float)Window.Size.Width / Window.Size.Height;
			Cameras.Set(MainCamera.ID, new Camera {
				Viewport   = new Rectangle(Point.Empty, size),
				Projection = Matrix4x4.CreatePerspectiveFieldOfView(
					60.0F * DEGREES_TO_RADIANS, aspectRatio, 0.1F, 100.0F),
			});
		}

		private void OnUpdate(double delta)
		{

		}

		private void OnRender(double delta)
		{
			GFX.Clear(Color.Indigo);
			_program.Use();

			var cameraEnumerator = Cameras.GetEnumerator();
			while (cameraEnumerator.MoveNext()) {
				var cameraID       = cameraEnumerator.CurrentEntityID;
				ref var camera     = ref cameraEnumerator.CurrentComponent;
				ref var view       = ref Transforms.GetRef(cameraID).Value;
				ref var projection = ref camera.Projection;
				// TODO: "view" probably needs to be inverted once the transform represents a normal
				//       entity transform instead of being manually created from Matrix4x4.LookAt.
				GFX.Viewport(camera.Viewport);

				var meshEnumerator = Meshes.GetEnumerator();
				while (meshEnumerator.MoveNext()) {
					var entityID      = meshEnumerator.CurrentEntityID;
					ref var mesh      = ref meshEnumerator.CurrentComponent;
					ref var modelView = ref Transforms.GetRef(entityID).Value;
					var meshInfo      = MeshManager.Find(mesh);
					_mvpUniform.Set(modelView * view * projection);
					meshInfo.Draw();
				}
			}

			Window.SwapBuffers();
		}
	}
}
