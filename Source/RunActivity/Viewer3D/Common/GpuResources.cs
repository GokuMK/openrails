// COPYRIGHT 2026 by the Open Rails project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using Microsoft.Xna.Framework.Graphics;
using ORTS.Common;

namespace Orts.Viewer3D
{
    /// <summary>
    /// Creates and fills graphics buffers as one unit of render-thread work (see <see cref="GpuDispatcher"/>).
    /// </summary>
    /// <remarks>
    /// Creating a buffer and setting its data are separate render-thread round trips on MonoGame DesktopGL when done
    /// from another thread. These helpers combine them; on WindowsDX they run inline.
    /// </remarks>
    public static class GpuResources
    {
        public static VertexBuffer CreateVertexBuffer<T>(GraphicsDevice graphicsDevice, Type vertexType, int vertexCount, BufferUsage usage, T[] data) where T : struct
        {
            return GpuDispatcher.Invoke(() =>
            {
                var buffer = new VertexBuffer(graphicsDevice, vertexType, vertexCount, usage);
                buffer.SetData(data);
                return buffer;
            });
        }

        public static VertexBuffer CreateVertexBuffer<T>(GraphicsDevice graphicsDevice, VertexDeclaration vertexDeclaration, int vertexCount, BufferUsage usage, T[] data) where T : struct
        {
            return GpuDispatcher.Invoke(() =>
            {
                var buffer = new VertexBuffer(graphicsDevice, vertexDeclaration, vertexCount, usage);
                buffer.SetData(data);
                return buffer;
            });
        }

        public static IndexBuffer CreateIndexBuffer<T>(GraphicsDevice graphicsDevice, Type indexType, int indexCount, BufferUsage usage, T[] data) where T : struct
        {
            return GpuDispatcher.Invoke(() =>
            {
                var buffer = new IndexBuffer(graphicsDevice, indexType, indexCount, usage);
                buffer.SetData(data);
                return buffer;
            });
        }

        public static IndexBuffer CreateIndexBuffer<T>(GraphicsDevice graphicsDevice, IndexElementSize indexElementSize, int indexCount, BufferUsage usage, T[] data) where T : struct
        {
            return GpuDispatcher.Invoke(() =>
            {
                var buffer = new IndexBuffer(graphicsDevice, indexElementSize, indexCount, usage);
                buffer.SetData(data);
                return buffer;
            });
        }
    }
}
