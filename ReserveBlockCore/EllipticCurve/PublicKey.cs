﻿namespace ReserveBlockCore.EllipticCurve
{
    public class PublicKey
    {

        public Point point { get; }

        public CurveFp curve { get; private set; }

        public PublicKey(Point point, CurveFp curve)
        {
            this.point = point;
            this.curve = curve;
        }

        public byte[] toString(bool encoded = false)
        {
            byte[] xString = BinaryAscii.stringFromNumber(point.x, curve.length());
            byte[] yString = BinaryAscii.stringFromNumber(point.y, curve.length());

            if (encoded)
            {
                return Der.combineByteArrays(new List<byte[]> {
                    BinaryAscii.binaryFromHex("00"),
                    BinaryAscii.binaryFromHex("04"),
                    xString,
                    yString
                });
            }
            return Der.combineByteArrays(new List<byte[]> {
                xString,
                yString
            });
        }

        public byte[] toDer()
        {
            int[] oidEcPublicKey = { 1, 2, 840, 10045, 2, 1 };
            byte[] encodedEcAndOid = Der.encodeSequence(
                new List<byte[]> {
                    Der.encodeOid(oidEcPublicKey),
                    Der.encodeOid(curve.oid)
                }
            );

            return Der.encodeSequence(
                new List<byte[]> {
                    encodedEcAndOid,
                    Der.encodeBitString(toString(true))
                }
            );
        }

        public string toPem()
        {
            return Der.toPem(toDer(), "PUBLIC KEY");
        }

        public static PublicKey fromPem(string pem)
        {
            return fromDer(Der.fromPem(pem));
        }

        public static PublicKey fromDer(byte[] der)
        {
            Tuple<byte[], byte[]> removeSequence1 = Der.removeSequence(der);
            byte[] s1 = removeSequence1.Item1;

            if (removeSequence1.Item2.Length > 0)
            {
                throw new ArgumentException(
                    "trailing junk after DER public key: " +
                    BinaryAscii.hexFromBinary(removeSequence1.Item2)
                );
            }

            Tuple<byte[], byte[]> removeSequence2 = Der.removeSequence(s1);
            byte[] s2 = removeSequence2.Item1;
            byte[] pointBitString = removeSequence2.Item2;

            Tuple<int[], byte[]> removeObject1 = Der.removeObject(s2);
            byte[] rest = removeObject1.Item2;

            Tuple<int[], byte[]> removeObject2 = Der.removeObject(rest);
            int[] oidCurve = removeObject2.Item1;

            if (removeObject2.Item2.Length > 0)
            {
                throw new ArgumentException(
                    "trailing junk after DER public key objects: " +
                    BinaryAscii.hexFromBinary(removeObject2.Item2)
                );
            }

            string stringOid = string.Join(",", oidCurve);

            if (!Curves.curvesByOid.ContainsKey(stringOid))
            {
                int numCurves = Curves.supportedCurves.Length;
                string[] supportedCurves = new string[numCurves];
                for (int i = 0; i < numCurves; i++)
                {
                    supportedCurves[i] = Curves.supportedCurves[i].name;
                }
                throw new ArgumentException(
                    "Unknown curve with oid [" +
                    string.Join(", ", oidCurve) +
                    "]. Only the following are available: " +
                    string.Join(", ", supportedCurves)
                );
            }

            CurveFp curve = Curves.curvesByOid[stringOid];

            Tuple<byte[], byte[]> removeBitString = Der.removeBitString(pointBitString);
            byte[] pointString = removeBitString.Item1;

            if (removeBitString.Item2.Length > 0)
            {
                throw new ArgumentException("trailing junk after public key point-string");
            }

            return fromString(Bytes.sliceByteArray(pointString, 2), curve.name);

        }

        public static PublicKey fromString(byte[] str, string curve = "secp256k1", bool validatePoint = true)
        {
            CurveFp curveObject = Curves.getCurveByName(curve);

            int baseLen = curveObject.length();

            if (str.Length != 2 * baseLen)
            {
                throw new ArgumentException("string length [" + str.Length + "] should be " + 2 * baseLen);
            }

            string xs = BinaryAscii.hexFromBinary(Bytes.sliceByteArray(str, 0, baseLen));
            string ys = BinaryAscii.hexFromBinary(Bytes.sliceByteArray(str, baseLen));

            Point p = new Point(
                BinaryAscii.numberFromHex(xs),
                BinaryAscii.numberFromHex(ys)
            );

            PublicKey publicKey = new PublicKey(p, curveObject);
            if (!validatePoint)
            {
                return publicKey;
            }
            if (p.isAtInfinity())
            {
                throw new ArgumentException("Public Key point is at infinity");
            }
            //if (!curveObject.contains(p))
            //{
            //    //FIX THIS
            //    throw new ArgumentException(
            //        "Point (" +
            //        p.x.ToString() + "," +
            //        p.y.ToString() + ") is not valid for curve " +
            //        curveObject.name
            //    );
            //}
            if (!EcdsaMath.multiply(p, curveObject.N, curveObject.N, curveObject.A, curveObject.P).isAtInfinity())
            {
                throw new ArgumentException(
                    "Point (" +
                    p.x.ToString() + "," +
                    p.y.ToString() + ") * " +
                    curveObject.name + ".N is not at infinity"
                );
            }
            return publicKey;
        }
    }
}
