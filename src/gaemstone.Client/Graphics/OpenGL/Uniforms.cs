using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Silk.NET.OpenGL;

namespace gaemstone.Client.Graphics
{
	public class Uniforms
		: IReadOnlyList<UniformInfo>
	{
		private readonly UniformInfo[] _activeUniforms;
		private readonly Dictionary<string, UniformInfo> _uniformsByName
			= new Dictionary<string, UniformInfo>();

		public int Count => _activeUniforms.Length;

		public UniformInfo this[int index] => _activeUniforms[index];
		public UniformInfo this[string name] => _uniformsByName[name];

		internal Uniforms(Program program)
		{
			GFX.GL.GetProgram(program.Handle, ProgramPropertyARB.ActiveUniformMaxLength, out var uniformMaxLength);
			var nameBuffer = new string(' ', uniformMaxLength);
			GFX.GL.GetProgram(program.Handle, ProgramPropertyARB.ActiveUniforms, out var uniformCount);
			_activeUniforms = new UniformInfo[uniformCount];
			for (uint uniformIndex = 0; uniformIndex < uniformCount; uniformIndex++) {
				UniformType type;
				GFX.GL.GetActiveUniform(program.Handle, uniformIndex, (uint)uniformMaxLength,
				                        out var length, out var size, out type, out nameBuffer);
				var name     = nameBuffer.Substring(0, (int)length);
				var location = GFX.GL.GetUniformLocation(program.Handle, name);
				var uniform  = new UniformInfo(uniformIndex, location, size, type, name);
				_activeUniforms[uniformIndex] = uniform;
				_uniformsByName[nameBuffer]   = uniform;
			}
		}

		public IEnumerator<UniformInfo> GetEnumerator()
			=> ((IEnumerable<UniformInfo>)_activeUniforms).GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> GetEnumerator();
	}

	public class UniformInfo
	{
		public uint Index { get; }
		public int Location { get; }
		public int Size { get; }
		public UniformType Type { get; }
		public string Name { get; }

		internal UniformInfo(uint index, int location, int size, UniformType type, string name)
			=> (Index, Location, Size, Type, Name) = (index, location, size, type, name);

		private int Ensure(int size, UniformType type, string convert)
		{
			if ((size != Size) || (type != Type)) throw new InvalidOperationException(
				$"Incompatible size and/or type to access as {convert} ({size} / {type})");
			return Location;
		}

		public UniformBool Bool
			=> new UniformBool(Ensure(1, UniformType.Bool, "bool"));
		public UniformInt Int
			=> new UniformInt(Ensure(1, UniformType.Int, "int"));
		public UniformFloat Float
			=> new UniformFloat(Ensure(1, UniformType.Float, "float"));
		public UniformVector3 Vector3
			=> new UniformVector3(Ensure(1, UniformType.FloatVec3, "Vector3"));
		public UniformVector3 Vector4
			=> new UniformVector3(Ensure(1, UniformType.FloatVec4, "Vector4"));
		public UniformMatrix4x4 Matrix4x4
			=> new UniformMatrix4x4(Ensure(1, UniformType.FloatMat4, "Matrix4x4"));
	}

	public readonly struct UniformBool
	{
		public int Location { get; }
		internal UniformBool(int location) => Location = location;
		public void Set(bool value) => GFX.GL.Uniform1(Location, value ? 1 : 0);
	}
	public readonly struct UniformInt
	{
		public int Location { get; }
		internal UniformInt(int location) => Location = location;
		public void Set(int value) => GFX.GL.Uniform1(Location, value);
	}
	public readonly struct UniformFloat
	{
		public int Location { get; }
		internal UniformFloat(int location) => Location = location;
		public void Set(float value) => GFX.GL.Uniform1(Location, value);
	}
	public readonly struct UniformVector3
	{
		public int Location { get; }
		internal UniformVector3(int location) => Location = location;
		public void Set(in Vector3 value) => GFX.GL.Uniform3(Location, 1, in value.X);
	}
	public readonly struct UniformVector4
	{
		public int Location { get; }
		internal UniformVector4(int location) => Location = location;
		public void Set(in Vector4 value) => GFX.GL.Uniform4(Location, 1, in value.X);
	}
	public readonly struct UniformMatrix4x4
	{
		public int Location { get; }
		internal UniformMatrix4x4(int location) => Location = location;
		public void Set(in Matrix4x4 value) => GFX.GL.UniformMatrix4(Location, 1, false, in value.M11);
	}
}
