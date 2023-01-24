using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Solvers;
using MathNet.Numerics.LinearAlgebra.Double.Solvers;
using OsMapDownloader.Coords;
using OsMapDownloader.Qct;
using Serilog;

namespace OsMapDownloader
{
    public static class PolynomialCalculator
    {
        //https://en.wikipedia.org/wiki/Polynomial_regression
        //https://en.wikipedia.org/wiki/Chebyshev_nodes

        public static QctGeographicalReferencingCoefficients Calculate(IProgress<double> progress, Osgb36Coordinate tl, Osgb36Coordinate br, int sampleSize, double pixelsPerMeter, CancellationToken cancellationToken = default(CancellationToken))
        {
            Log.Debug("Calculating Geographical Referencing Polynomials, this will take a while");

            Osgb36Coordinate[] sampleCoords = GetSampleCoordinates(tl, br, sampleSize, cancellationToken);
            progress.Report(1);
            (Wgs84Coordinate, Coordinate)[] convertedCoords = sampleCoords.WithCancellation(cancellationToken).Select(coord => (coord.ToWgs84Accurate(), GetPixelsFromOsgb(coord, tl, pixelsPerMeter))).ToArray();
            progress.Report(2);

            Vector<double> lonC = DoPolynomialRegression(convertedCoords.Select(coord => new SampleCoordinate(coord.Item2.X, coord.Item2.Y, coord.Item1.Longitude)), sampleSize, cancellationToken);
            progress.Report(3);
            Log.Debug("Lon Coefficients {lonC}", lonC);
            Vector<double> latC = DoPolynomialRegression(convertedCoords.Select(coord => new SampleCoordinate(coord.Item2.X, coord.Item2.Y, coord.Item1.Latitude)), sampleSize, cancellationToken);
            progress.Report(4);
            Log.Debug("Lat Coefficients {latC}", latC);
            Vector<double> xC = DoPolynomialRegression(convertedCoords.Select(coord => new SampleCoordinate(coord.Item1.Longitude, coord.Item1.Latitude, coord.Item2.X)), sampleSize, cancellationToken);
            progress.Report(5);
            Log.Debug("X Coefficients {xC}", xC);
            Vector<double> yC = DoPolynomialRegression(convertedCoords.Select(coord => new SampleCoordinate(coord.Item1.Longitude, coord.Item1.Latitude, coord.Item2.Y)), sampleSize, cancellationToken);
            progress.Report(6);
            Log.Debug("Y Coefficients {yC}", yC);

            Log.Debug("Calculated Geographical Referencing Polynomials");
            
            return new QctGeographicalReferencingCoefficients(
                xC[0], xC[2], xC[1], xC[5], xC[4], xC[3], xC[9], xC[8], xC[7], xC[6],
                yC[0], yC[2], yC[1], yC[5], yC[4], yC[3], yC[9], yC[8], yC[7], yC[6],
                latC[0], latC[1], latC[2], latC[3], latC[4], latC[5], latC[6], latC[7], latC[8], latC[9],
                lonC[0], lonC[1], lonC[2], lonC[3], lonC[4], lonC[5], lonC[6], lonC[7], lonC[8], lonC[9]);
        }

        private struct SampleCoordinate
        {
            public SampleCoordinate(double x, double y, double returnVal)
            {
                X = x;
                Y = y;
                ReturnVal = returnVal;
            }
            public double X;
            public double Y;
            public double ReturnVal;
        }

        private static Coordinate GetPixelsFromOsgb(Osgb36Coordinate coord, Osgb36Coordinate tl, double pixelsPerMeter)
        {
            //400px for 1000m
            return new Coordinate((coord.Easting - tl.Easting) * pixelsPerMeter, (tl.Northing - coord.Northing) * pixelsPerMeter);
        }

        private static double[] GetChebyshevNodes(double start, double end, int n)
        {
            double[] nodes = new double[n];
            for (int k = 1; k <= n; k++)
            {
                nodes[k - 1] = (0.5 * (start + end)) + (0.5 * (end - start) * Math.Cos(((double)(2 * k - 1) / (double)(2 * n)) * Math.PI));
            }
            return nodes;
        }

        private static Osgb36Coordinate[] GetSampleCoordinates(Osgb36Coordinate tlConstraint, Osgb36Coordinate brConstraint, int sampleSize, CancellationToken cancellationToken = default(CancellationToken))
        {
            double[] x = GetChebyshevNodes(tlConstraint.Easting, brConstraint.Easting, sampleSize);
            double[] y = GetChebyshevNodes(brConstraint.Northing, tlConstraint.Northing, sampleSize);
            Osgb36Coordinate[] coordinates = new Osgb36Coordinate[x.Length * y.Length];
            for (int row = 0; row < y.Length; row++)
            {
                for (int column = 0; column < x.Length; column++)
                {
                    coordinates[row * x.Length + column] = new Osgb36Coordinate(x[column], y[row]);
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            return coordinates;
        }

        private static Vector<double> DoPolynomialRegression(IEnumerable<SampleCoordinate> coords, int sampleSize, CancellationToken cancellationToken = default(CancellationToken))
        {
            IEnumerator<SampleCoordinate> coordEnumerable = coords.GetEnumerator();
            Matrix<double> X = CreateMatrix.Dense<double>(sampleSize * sampleSize, 10);
            Vector<double> y = CreateVector.Dense<double>(sampleSize * sampleSize);
            cancellationToken.ThrowIfCancellationRequested();
            for (int i = 0; i < sampleSize * sampleSize; i++)
            {
                coordEnumerable.MoveNext();
                SampleCoordinate coord = coordEnumerable.Current;
                X[i, 0] = 1;
                X[i, 1] = coord.X;
                X[i, 2] = coord.Y;
                X[i, 3] = coord.X * coord.X;
                X[i, 4] = coord.X * coord.Y;
                X[i, 5] = coord.Y * coord.Y;
                X[i, 6] = coord.X * coord.X * coord.X;
                X[i, 7] = coord.X * coord.X * coord.Y;
                X[i, 8] = coord.X * coord.Y * coord.Y;
                X[i, 9] = coord.Y * coord.Y * coord.Y;
                y[i] = coord.ReturnVal;
            }
            cancellationToken.ThrowIfCancellationRequested();

            MlkBiCgStab cgSolver = new MlkBiCgStab();
            Vector<double> solutionVector = X.TransposeThisAndMultiply(y);
            cancellationToken.ThrowIfCancellationRequested();
            Matrix<double> coefficientMatrix = X.TransposeThisAndMultiply(X);
            cancellationToken.ThrowIfCancellationRequested();
            Vector<double> solutions = CreateVector.Dense<double>(10);
            cancellationToken.ThrowIfCancellationRequested();
            cgSolver.Solve(coefficientMatrix, solutionVector, solutions, new Iterator<double>(), new ILU0Preconditioner());
            cancellationToken.ThrowIfCancellationRequested();
            return solutions;
        }
    }
}
