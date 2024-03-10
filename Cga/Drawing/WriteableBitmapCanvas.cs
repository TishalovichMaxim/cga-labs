using System.Numerics;
using System.Security.Cryptography.Xml;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Cga.Graphics;
using Cga.LinearAlgebra;
using GlmNet;

namespace Cga.Drawing;

delegate void DrawHorizontalLine(int a);

public class WriteableBitmapCanvas
{
    private static readonly int BytesPerPixel = 4;

    private readonly WriteableBitmap _writeableBitmap;

    private readonly byte[] _pixelsData;

    private readonly float[] _zBuffer;

    private int Round(float num) => (int)(num + 0.5);

    public int Width => _writeableBitmap.PixelWidth;

    public int Height => _writeableBitmap.PixelHeight;


    public WriteableBitmapCanvas(WriteableBitmap writeableBitmap)
    {
        _writeableBitmap = writeableBitmap;
        int nPixels = _writeableBitmap.PixelWidth * _writeableBitmap.PixelHeight;
        int bytesLen = nPixels * BytesPerPixel;
        _pixelsData = new byte[bytesLen];
        _zBuffer = new float[nPixels];
        //Array.Fill(_zBuffer, 1.1f);
    }

    public void DrawPixel(Color color, byte[] pixelsData, int stride, (int x, int y, float z) pos)
    {
        int pixelNumber = pos.y * _writeableBitmap.PixelWidth + pos.x;

        if (pos.z > _zBuffer[pixelNumber])
        {
            return;
        }

        _zBuffer[pixelNumber] = pos.z;

        DrawPixel(color, pixelsData, stride, (pos.x, pos.y));
    }

    public void Clear()
    {
        Array.Clear(_pixelsData, 0, _pixelsData.Length);
    }

    public void Clear(Color color)
    {

        Array.Fill(_pixelsData, (byte)0);
        //for (int i = 0; i < _writeableBitmap.Height; i++)
        //{
        //    for (int j = 0; j < _writeableBitmap.Width; j++)
        //    {
        //        DrawPixel(color, _pixelsData, Width, (j, i, -1.0f));
        //    }
        //}

        Array.Fill(_zBuffer, 1.1f);
    }

    public void Swap()
    {
        Int32Rect dirtyRect = new Int32Rect(
            0, 0, _writeableBitmap.PixelWidth, _writeableBitmap.PixelHeight);

        _writeableBitmap.WritePixels(
            dirtyRect,
            _pixelsData,
            _writeableBitmap.PixelWidth * BytesPerPixel,
            0
            );
    }

    public void DrawLine(Color color, byte[] pixelData, int stride, (int, int, float) from, (int, int, float) to)
    {
        int dx = to.Item1 - from.Item1;
        int dy = to.Item2 - from.Item2;

        int maxDelta = Math.Max(Math.Abs(dx), Math.Abs(dy));
        if (maxDelta == 0)
        {
            return;
        }

        float stepX = ((float)dx) / maxDelta;
        float stepY = ((float)dy) / maxDelta;
        float stepZ = (to.Item3 - from.Item3) / maxDelta;

        for (int i = 0; i <= maxDelta; i++)
        {
            int x = Round(from.Item1 + stepX * i);
            int y = Round(from.Item2 + stepY * i);
            float z = from.Item3 + stepZ * i;
            DrawPixel(
                color,
                pixelData,
                stride,
                (x, y, z)
                );
        }
    }

    public void DrawLine(Color color, vec3 from, vec3 to)
    {
        DrawLine(
            color,
            _pixelsData,
            Width,
            (from.x.NearInt(), from.y.NearInt(), from.z),
            (to.x.NearInt(), to.y.NearInt(), to.z)
            );
    }

    public void DrawPixel(Color color, byte[] pixelsData, int stride, (int x, int y) pos)
    {
        int offset = (pos.y * stride + pos.x) * BytesPerPixel;

        pixelsData[offset] = color.B;
        pixelsData[offset + 1] = color.G;
        pixelsData[offset + 2] = color.R;
        pixelsData[offset + 3] = color.A;
    }

    public void DirtyScanLine(Vector3 p1, Vector3 p2, Vector3 p3, Color color)
    {
        if (p2.Y < p1.Y)
        {
            (p1, p2) = (p2, p1);
        }

        if (p2.Y < p3.Y)
        {
            (p2, p3) = (p3, p2);
        }

        if (p1.Y > p3.Y)
        {
            (p1, p3) = (p3, p1);
        }

        int p1x = (int)p1.X;
        int p1y = (int)p1.Y;

        int p2x = (int)p2.X;
        int p2y = (int)p2.Y;

        int p3x = (int)p3.X;
        int p3y = (int)p3.Y;

        if (p1y == p2y && p2y == p3y)
        {
            return;
        }

        if (p1y == p3y)
        {
            for (int y = p1y; y <= p2y; y++)
            {
                float t = ((float)(y - p1y)) / (p2y - p1y);

                int left = (int)(p3x + t*(p2x - p3x));
                int right = (int)(p1x + t*(p2x - p1x));

                float zLeft = p3.Z + (p2.Z - p3.Z) * t;
                float zRight = p1.Z + (p2.Z - p1.Z) * t;

                if (left > right)
                {
                    (left, right) = (right, left);
                    (zLeft, zRight) = (zRight, zLeft);
                }
                
                if (left != right)
                {
                    for (int x = left; x <= right; x++)
                    {
                        float z = zLeft + (zRight - zLeft)*(((float)(x - left))/(right - left));
                        DrawPixel(color, _pixelsData, Width, (x, y, z));
                    }
                }
                else
                {
                    DrawPixel(color, _pixelsData, Width, (left, y, zLeft));
                }
            }
        }
        else if (p3y == p2y)
        {
            for (int y = p1y; y <= p2y; y++)
            {
                float t = ((float)(y - p1y))/(p2y - p1y);

                int left = (int)(p1x + (p3x - p1x) * t);
                int right= (int)(p1x + (p2x - p1x) * t);

                float zLeft = p1.Z + (p3.Z - p1.Z)*t;
                float zRight = p1.Z + (p2.Z - p1.Z)*t;

                if (left > right)
                {
                    (left, right) = (right, left);
                    (zLeft, zRight) = (zRight, zLeft);
                }

                if (left != right)
                {
                    for (int x = left; x <= right; x++)
                    {
                        float z = zLeft + (zRight - zLeft)*(((float)(x - left))/(right - left));
                        DrawPixel(color, _pixelsData, Width, (x, y, z));
                    }
                }
                else
                {
                    DrawPixel(color, _pixelsData, Width, (left, y, zLeft));
                }
            }
        }
        else
        {
            for (int y = p1y; y <= p3y; y++)
            {
                float tLeft = ((float)(y - p1y))/(p3y - p1y);
                float tRight = ((float)(y - p1y))/(p2y - p1y);

                int left = (int)(p1x + (p3x - p1x)*tLeft);
                int right = (int)(p1x + (p2x - p1x)*tRight);

                float zLeft = p1.Z + (p3.Z - p1.Z) * tLeft;
                float zRight = p1.Z + (p2.Z - p1.Z) * tRight;

                if (left > right)
                {
                    (left, right) = (right, left);
                    (zLeft, zRight) = (zRight, zLeft);
                }

                if (left != right)
                {
                    for (int x = left; x <= right; x++)
                    {
                        float z = zLeft + (zRight - zLeft)*(((float)(x - left))/(right - left));
                        DrawPixel(color, _pixelsData, Width, (x, y, z));
                    }
                }
                else
                {
                    DrawPixel(color, _pixelsData, Width, (left, y, zLeft));
                }
            }

            for (int y = p3y + 1; y <= p2y; y++)
            {
                float tLeft = ((float)(y - p3y))/(p2y - p3y);
                float tRight = ((float)(y - p1y))/(p2y - p1y);

                int left = (int)(p3x + (p2x - p3x)*tLeft);
                int right = (int)(p1x + (p2x - p1x)*tRight);

                float zLeft = p3.Z + (p2.Z - p3.Z) * tLeft;
                float zRight = p1.Z + (p2.Z - p1.Z) * tRight;

                if (left > right)
                {
                    (left, right) = (right, left);
                    (zLeft, zRight) = (zRight, zLeft);
                }

                if (left != right)
                {
                    for (int x = left; x <= right; x++)
                    {
                        float z = zLeft + (zRight - zLeft)*(((float)(x - left))/(right - left));
                        DrawPixel(color, _pixelsData, Width, (x, y, z));
                    }
                }
                else
                {
                    DrawPixel(color, _pixelsData, Width, (left, y, zLeft));
                }
            }
        }
    }
}