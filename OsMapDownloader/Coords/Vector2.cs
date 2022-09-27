using System;

namespace OsMapDownloader.Coords
{
    public class Vector2<T> where T : Vector2<T>, new()
    {
        protected double x { get; private set; } = 0;
        protected double y { get; private set; } = 0;

        protected Vector2() { }
        public Vector2(double x, double y)
        {
            this.x = x;
            this.y = y;
        }

        public static T operator +(Vector2<T> a, Vector2<T> b) => new T() { x = a.x + b.x, y = a.y + b.y };
        public static T operator -(Vector2<T> a, Vector2<T> b) => new T() { x = a.x - b.x, y = a.y - b.y };
        public static T operator *(Vector2<T> a, Vector2<T> b) => new T() { x = a.x * b.x, y = a.y * b.y };
        public static T operator /(Vector2<T> a, Vector2<T> b) => new T() { x = a.x / b.x, y = a.y / b.y };
        public static T operator *(Vector2<T> a, double b) => new T() { x = a.x * b, y = a.y * b };
        public static T operator /(Vector2<T> a, double b) => new T() { x = a.x / b, y = a.y / b };
    }
}
