using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OsMapDownloader.Border;

namespace OsMapDownloader.Qct
{
    public class OpenGLManager : OpenTK.Windowing.Desktop.GameWindow
    {
        private class WorkItem
        {
            public WorkItem(byte[][] imageData, int imageWidth, int imageHeight, int canvasWidth, int canvasHeight, Matrix4 transformMatrix, Matrix4 stencilTransformMatrix, TaskCompletionSource<byte[]> result)
            {
                ImageData = imageData;
                ImageWidth = imageWidth;
                ImageHeight = imageHeight;
                CanvasWidth = canvasWidth;
                CanvasHeight = canvasHeight;
                TransformMatrix = transformMatrix;
                StencilTransformMatrix = stencilTransformMatrix;
                Result = result;
            }

            public byte[][] ImageData { get; set; }
            public int ImageWidth { get; set; }
            public int ImageHeight { get; set; }
            public int CanvasWidth { get; set; }
            public int CanvasHeight { get; set; }
            public Matrix4 TransformMatrix { get; set; }
            public Matrix4 StencilTransformMatrix { get; set; }
            public TaskCompletionSource<byte[]> Result { get; set; }
        }

        private BlockingCollection<WorkItem> _workQueue = new BlockingCollection<WorkItem>();
        private bool _hasCreatedTexture = false;

        private int _frameBuffer;
        private int _colourTexture;
        private int _depthStencilBuffer;

        private readonly float[] _vertices =
{
            // Position         Texture coordinates
             1.0f,  1.0f, 0.0f, 1.0f, 1.0f, // top right
             1.0f, -1.0f, 0.0f, 1.0f, 0.0f, // bottom right
            -1.0f, -1.0f, 0.0f, 0.0f, 0.0f, // bottom left
            -1.0f,  1.0f, 0.0f, 0.0f, 1.0f  // top left
        };
        private readonly uint[] _indices =
        {
            0, 1, 3,
            1, 2, 3
        };

        private int _vertexArrayObject;
        private int _vertexBufferObject;
        private int _elementBufferObject;

        private int _shaderProgram;
        private int _matrixUniformLocation;

        private int _stencilVertexArray;
        private int _stencilVertexBuffer;
        private int _stencilElementBuffer;

        private int _stencilShaderProgram;
        private int _stencilMatrixUniformLocation;

        private int _stencilIndicesLength;

        private int _texture;

        static OpenGLManager()
        {
            OpenTK.Windowing.Desktop.GLFWProvider.CheckForMainThread = false;
        }

        public OpenGLManager() : base(
            OpenTK.Windowing.Desktop.GameWindowSettings.Default,
            new OpenTK.Windowing.Desktop.NativeWindowSettings()
            {
                Size = new Vector2i(1, 1),
                Title = "OsMapDownloader Dummy Window",
                Flags = ContextFlags.ForwardCompatible | ContextFlags.Offscreen,
                WindowBorder = WindowBorder.Fixed,
                IsEventDriven = true,
                StartVisible = false
            })
        {

        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            GL.DeleteFramebuffer(_frameBuffer);
            GL.DeleteTexture(_colourTexture);
            GL.DeleteRenderbuffer(_depthStencilBuffer);
        }

        public void Init(MapArea area)
        {
            _hasCreatedTexture = false;
            Context.MakeCurrent();
            GL.ClearColor(Color4.White);

            //Create framebuffer
            _frameBuffer = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _frameBuffer);
            //Create texture for framebuffer's colours
            _colourTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, _colourTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 64, 64, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            //Attach texture to framebuffer
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _colourTexture, 0);
            //Create renderbuffer for framebuffer's depth and stencil
            _depthStencilBuffer = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthStencilBuffer);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, 64, 64);
            //Attach renderbuffer to framebuffer
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depthStencilBuffer);
            //Ensure framebuffer works
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            {
                throw new Exception("Framebuffer is not complete");
            }
            //Unbind framebuffer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            //TEXTURED SQUARE
            //Create and bind vertex array object
            _vertexArrayObject = GL.GenVertexArray();
            GL.BindVertexArray(_vertexArrayObject);
            //Create and bind vertex buffer object
            _vertexBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsageHint.StaticDraw);
            //Create and bind element buffer object
            _elementBufferObject = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _elementBufferObject);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint), _indices, BufferUsageHint.StaticDraw);

            //Create and compile vertex shader
            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, @"#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aTexCoord;
out vec2 texCoord;

uniform mat4 transformMat;

void main(void)
{
    texCoord = aTexCoord;

    gl_Position = vec4(aPosition, 1.0) * transformMat;
}");
            CompileShader(vertexShader);

            //Create and compile fragment shader
            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, @"#version 330

out vec4 outputColor;
in vec2 texCoord;

uniform sampler2D texture0;

void main()
{
    vec4 texColor = texture(texture0, texCoord);
    if (texColor.a <= 0.9)
        discard;
    outputColor = texColor;
}");
            CompileShader(fragmentShader);

            //Create program
            _shaderProgram = GL.CreateProgram();
            GL.AttachShader(_shaderProgram, vertexShader);
            GL.AttachShader(_shaderProgram, fragmentShader);
            LinkProgram(_shaderProgram);

            //Cache matrix uniform
            _matrixUniformLocation = GL.GetUniformLocation(_shaderProgram, "transformMat");

            //Delete individual shaders now that we've created the program from them
            GL.DetachShader(_shaderProgram, vertexShader);
            GL.DetachShader(_shaderProgram, fragmentShader);
            GL.DeleteShader(vertexShader);
            GL.DeleteShader(fragmentShader);

            //Pass vertex coordinates
            int vertexLocation = GL.GetAttribLocation(_shaderProgram, "aPosition");
            GL.EnableVertexAttribArray(vertexLocation);
            GL.VertexAttribPointer(vertexLocation, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);

            //Pass texture coordinates
            int texCoordLocation = GL.GetAttribLocation(_shaderProgram, "aTexCoord");
            GL.EnableVertexAttribArray(texCoordLocation);
            GL.VertexAttribPointer(texCoordLocation, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));

            //Set samplers
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "texture0"), 0);

            //STENCIL
            //Create and bind vertex array object
            _stencilVertexArray = GL.GenVertexArray();
            GL.BindVertexArray(_stencilVertexArray);
            //Create and bind vertex buffer object
            _stencilVertexBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _stencilVertexBuffer);
            float[] stencilVertices = area.GetOpenGLVertices();
            GL.BufferData(BufferTarget.ArrayBuffer, stencilVertices.Length * sizeof(float), stencilVertices, BufferUsageHint.StaticDraw);
            //Create and bind element buffer object
            _stencilElementBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _stencilElementBuffer);
            uint[] stencilIndices = area.GetOpenGLIndices();
            _stencilIndicesLength = stencilIndices.Length;
            GL.BufferData(BufferTarget.ElementArrayBuffer, _stencilIndicesLength * sizeof(uint), stencilIndices, BufferUsageHint.StaticDraw);

            //Create and compile vertex shader
            int stencilVertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(stencilVertexShader, @"#version 330 core

layout(location = 0) in vec3 aPosition;

uniform mat4 transformMat;

void main(void)
{
    gl_Position = vec4(aPosition, 1.0) * transformMat;
}");
            CompileShader(stencilVertexShader);

            //Create and compile fragment shader
            int stencilFragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(stencilFragmentShader, @"#version 330

out vec4 outputColor;

void main()
{
    outputColor = vec4(1.0f, 0.0f, 0.0f, 1.0f);
}");
            CompileShader(stencilFragmentShader);

            //Create program
            _stencilShaderProgram = GL.CreateProgram();
            GL.AttachShader(_stencilShaderProgram, stencilVertexShader);
            GL.AttachShader(_stencilShaderProgram, stencilFragmentShader);
            LinkProgram(_stencilShaderProgram);

            //Cache matrix uniform
            _stencilMatrixUniformLocation = GL.GetUniformLocation(_stencilShaderProgram, "transformMat");

            //Delete individual shaders now that we've created the program from them
            GL.DetachShader(_stencilShaderProgram, stencilVertexShader);
            GL.DetachShader(_stencilShaderProgram, stencilFragmentShader);
            GL.DeleteShader(stencilVertexShader);
            GL.DeleteShader(stencilFragmentShader);

            //Pass vertex coordinates
            int stencilVertexLocation = GL.GetAttribLocation(_stencilShaderProgram, "aPosition");
            GL.EnableVertexAttribArray(stencilVertexLocation);
            GL.VertexAttribPointer(stencilVertexLocation, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);

            //Create texture and set parameters
            _texture = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            //Configure what to do when stencil test passes
            GL.Enable(EnableCap.StencilTest);
            GL.StencilOp(StencilOp.Keep, StencilOp.Replace, StencilOp.Replace);

            //Specify clear values
            GL.ClearColor(Color4.White);
            GL.ClearStencil(0);
        }

        private void CompileShader(int shader)
        {
            GL.CompileShader(shader);
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int code);
            if (code != (int)All.True)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
                throw new Exception($"An error occurred while compiling shader {shader}:\n{infoLog}");
            }
        }

        private void LinkProgram(int program)
        {
            GL.LinkProgram(program);
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int code);
            if (code != (int)All.True)
            {
                string infoLog = GL.GetProgramInfoLog(program);
                throw new Exception($"An error occurred while linking program:\n{infoLog}");
            }
        }

        public byte[] RenderToFramebuffer(byte[][] images, int imageWidth, int imageHeight, int canvasWidth, int canvasHeight, Matrix4 transformMat, Matrix4 stencilTransformMat)
        {
            //Bind to our framebuffer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _frameBuffer);
            GL.Viewport(0, 0, 64, 64);

            //1st pass - fill stencil buffer with 1s if it's in the area
            //-----------------------------------------
            //Configure stencil buffer
            GL.StencilFunc(StencilFunction.Always, 1, 0xFF); //It's always drawn regardless of value in stencil buffer
            GL.StencilMask(0xFF); //Allow writing

            //Clear framebuffer and stencil buffer
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.StencilBufferBit);

            //Bind vertex array
            GL.BindVertexArray(_stencilVertexArray);

            //Enable shader
            GL.UseProgram(_stencilShaderProgram);

            //Pass in matrix
            GL.UniformMatrix4(_stencilMatrixUniformLocation, true, ref stencilTransformMat);

            //Draw stuff to the framebuffer and stencil buffer
            GL.DrawElements(PrimitiveType.Triangles, _stencilIndicesLength, DrawElementsType.UnsignedInt, 0);

            //2nd pass - draw to framebuffer only if there's a 1 in the stencil buffer
            //-----------------------------------------
            //Configure stencil buffer
            GL.StencilFunc(StencilFunction.Notequal, 0, 0xFF); //It's only drawn if stencil buffer isn't 0
            GL.StencilMask(0x00); //Disable writing

            //Clear framebuffer
            GL.Clear(ClearBufferMask.ColorBufferBit);

            //Bind vertex array
            GL.BindVertexArray(_vertexArrayObject);
            //Replace texture
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            if (!_hasCreatedTexture)
            {
                //Create blank texture the size of the canvas
                byte[] canvasImage = new byte[canvasWidth * canvasHeight * 4];
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, canvasWidth, canvasHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, canvasImage);
                _hasCreatedTexture = true;
            }

            //Copy each image to the texture
            int i = 0;
            for (int yOffset = 0; yOffset < imageHeight; yOffset += 256)
            {
                for (int xOffset = 0; xOffset < imageWidth; xOffset += 256)
                {
                    GL.TexSubImage2D(TextureTarget.Texture2D, 0, xOffset, yOffset, 256, 256, PixelFormat.Rgba, PixelType.UnsignedByte, images[i]);
                    i++;
                }
            }

            //Enable shader
            GL.UseProgram(_shaderProgram);

            //Pass in matrix
            GL.UniformMatrix4(_matrixUniformLocation, true, ref transformMat);

            //Draw stuff to the framebuffer
            GL.DrawElements(PrimitiveType.Triangles, _indices.Length, DrawElementsType.UnsignedInt, 0);

            //Read bytes from framebuffer
            byte[] data = new byte[16384];
            GL.ReadPixels(0, 0, 64, 64, PixelFormat.Rgba, PixelType.UnsignedByte, data);
            //Unbind framebuffer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            return data;
        }

        public void ProcessTilesUntilTaskComplete(Task task)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            task.ContinueWith(_ => cts.Cancel());
            CancellationToken cancelToken = cts.Token;
            //Keep taking stuff from queue until cancelToken cancels
            while (!cancelToken.IsCancellationRequested)
            {
                try
                {
                    WorkItem work = _workQueue.Take(cancelToken);
                    byte[] data = RenderToFramebuffer(work.ImageData, work.ImageWidth, work.ImageHeight, work.CanvasWidth, work.CanvasHeight, work.TransformMatrix, work.StencilTransformMatrix);
                    work.Result.SetResult(data);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public async Task<byte[]> ProcessTileAsync(byte[][] images, int imageWidth, int imageHeight, int canvasWidth, int canvasHeight, double centerX, double centerY, double tileWidth, double tileHeight, double rotation, double centerEastings, double centerNorthings, double kmWidth, double kmHeight)
        {
            //NORMAL TRANSFORM MATRIX
            //Scale first to make crop correct size, then translate to put it into frame, then rotate

            //Create scale matrix
            double xScale = canvasWidth / tileWidth;
            double yScale = canvasHeight / tileHeight;
            Matrix4 scaleMat = Matrix4.CreateScale((float)xScale, (float)yScale, 1.0f);

            //Create translation matrix (up = positive, left = positive)
            double xTrans = (canvasWidth - centerX - canvasWidth / 2) / canvasWidth * xScale * 2; //Take pixels to move, divide by width, then times by xScale and double
            double yTrans = (canvasHeight - centerY - canvasHeight / 2) / canvasHeight * yScale * 2;
            Matrix4 translateMat = Matrix4.CreateTranslation((float)xTrans, (float)yTrans, 0.0f);

            //Create rotation matrix
            Matrix4 rotationMat = Matrix4.CreateRotationZ((float)rotation);

            Matrix4 transformMat = scaleMat * translateMat * rotationMat;

            //STENCIL TRANSFORM MATRIX
            //Transform based on kilometers, then scale
            Matrix4 stencilTranslateMat = Matrix4.CreateTranslation((float)(-centerEastings / 1000), (float)(-centerNorthings / 1000), 0.0f);
            Matrix4 stencilScaleMat = Matrix4.CreateScale((float)(2 / kmWidth), (float)(-2 / kmHeight), 1.0f);
            Matrix4 stencilTransformMat = stencilTranslateMat * stencilScaleMat;

            TaskCompletionSource<byte[]> tcs = new TaskCompletionSource<byte[]>();
            _workQueue.Add(new WorkItem(images, imageWidth, imageHeight, canvasWidth, canvasHeight, transformMat, stencilTransformMat, tcs));
            byte[] data = await tcs.Task;
            return data;
        }

        public override void Close()
        {
            Context.MakeNoneCurrent();
            base.Close();
        }
    }
}
