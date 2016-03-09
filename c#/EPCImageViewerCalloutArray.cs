namespace ImageExtractorFV4
{
    public class EPCImageViewerCalloutArray
    {
        private int[] x;
        private int[] y;
        private string[] c;
        private int count;

        public void ClearCalloutArray()
        {
            c = null;
            x = null;
            y = null;
            count = 0;
        }

        public void Allocate(int calloutCount)
        {
            count = calloutCount;
            x = new int[calloutCount];
            y = new int[calloutCount];
            c = new string[calloutCount];
        }

        public int GetX(int index)
        {
            return x[index];
        }

        public int GetY(int index)
        {
            return y[index];
        }

        public string GetCalloutDesc(int index)
        {
            return c[index];
        }

        public void AddToX(int value, int index)
        {
            x[index] = value;
        }

        public void AddToY(int value, int index)
        {
            y[index] = value;
        }

        public void AddToCalloutDesc(string value, int index)
        {
            value = value.Trim();
            c[index] = value;
        }

        public int GetCount()
        {
            return count;
        }
    }
}