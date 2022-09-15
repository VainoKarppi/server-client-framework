using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// https://gist.github.com/maca134/94b78268f6727f5967f8
/*
    //----- FROM ARMA TO CS
    string code = @"[""test"",true,[""ttt"",123,false]]";
    var test = ArmaArray.Unserialize(code);
    var firstItem = test[0];
    Log.Write(firstItem);


    //----- FROM CS TO ARMA
    ArmaArray arr = new ArmaArray();
    arr.Add("test");
    arr.Add(true);
    arr.Add(123);

    // Add sub array
    ArmaArray newArr = new ArmaArray();
    newArr.Add("asd");
    newArr.Add(100);
    newArr.Add(true);
    arr.Add(newArr);

    string toArma = ArmaArray.Serialize(arr);
    Log.Write(toArma);

*/

public class ArmaArray : List<object> {


    // From ARMA TO CS
    public static ArmaArray UnserializeArray(string[] args) {
        ArmaArray array = new ArmaArray();

        foreach (string input in args) {
            array.Add(ConvertToAny(input));
        }
        return array;
    }
    public static ArmaArray Unserialize(string args) {
        if (args.ElementAt(0) != '[') 
            args = "[" + args + "]";
            
        args = args.Substring(1);

        ArmaArray array = new ArmaArray();
        char[] nums = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.', '-' };
        for (int i = 0; i < args.Length; i++) {
            if (args[i] == '[') {
                StringBuilder str = new StringBuilder();
                int inArr = 0;
                while (true) {
                    if (args[i] == '[') {
                        inArr++;
                    }
                    if (args[i] == ']') {
                        inArr--;
                    }
                    str.Append(args[i]);
                    i++;
                    if (inArr == 0) {
                        break;
                    }
                }
                List<object> innerArray = Unserialize(str.ToString());
                array.Add(innerArray);
            } else if (args[i] == '"') {
                StringBuilder str = new StringBuilder();
                bool isEnd = false;
                i++;
                while (true) {
                    try {
                        if (args[i] == '"') {
                            isEnd = !isEnd;
                        }
                    } catch {
                        break;
                    }
                    if (isEnd && (args[i] == ',' || args[i] == ']')) {
                        break;
                    }
                    str.Append(args[i]);
                    i++;
                }
                array.Add(str.ToString().TrimEnd('"'));
            } else if (nums.Contains(args[i])) {
                StringBuilder str = new StringBuilder();
                bool isFloat = false;
                while (nums.Contains(args[i])) {
                    if (args[i] == '.')
                        isFloat = true;
                    str.Append(args[i]);
                    i++;
                }
                if (isFloat) {
                    double num = Convert.ToDouble(str.ToString());
                    array.Add(num);
                } else {
                    int num = Convert.ToInt32(str.ToString());
                    array.Add(num);
                }
            } else if (Substring(args, i, 4).ToLower() == "true") {
                array.Add(true);
                i = i + 4;
            } else if (Substring(args, i, 5).ToLower() == "false") {
                array.Add(false);
                i = i + 5;
            }
        }
        return array;
    }

    // FROM ARMA to CS
    public static string Serialize(ArmaArray array) {
        StringBuilder data = new StringBuilder();
        data.Append("[");
        if (array == null) {
            data.Append("]");
            return data.ToString();
        }
        
        foreach (object d in array) {
            if (d is string) {
                data.Append("\"");
                string s = d as string;
                data.Append(s);
                data.Append("\"");
            } else if (d is int || d is double || d is bool || d is short || d is byte) {
                data.Append(d.ToString());
            } else if (d is ArmaArray || d is object[]) {
                ArmaArray a = d as ArmaArray;
                data.Append(Serialize(a));
            }
            data.Append(",");
        }
        if (data[data.Length - 1] == ',') {
            data.Length--;
        }
        data.Append("]");
        return data.ToString();
    }

    private static string Substring(string input, int start, int length) {
        int inputLength = input.Length;
        if (start + length >= inputLength) {
            return input.Substring(start);
        }
        return input.Substring(start, length);
    }

    public int AsInt(int index) {
        try {
            return Convert.ToInt32(this[index]);
        } catch {
            return -1;
        }
    }

    public float AsFloat(int index) {
        try {
            return Convert.ToSingle(this[index]);
        } catch {
            return -1f;
        }
    }

    public ArmaArray AsArray(int index) {
        return (this[index] as ArmaArray);
    }

    public string AsString(int index) {
        return (this[index] as string);
    }

    public bool AsBool(int index) {
        try {
            return Convert.ToBoolean(this[index]);
        } catch {
            return false;
        }
    }

    public override string ToString() {
        return Serialize(this);
    }

    public long AsLong(int index) {
        try {
            return Convert.ToInt64(this[index]);
        } catch {
            return -1;
        }
    }


    public static object ConvertToAny(string input)
    {
        int i;
        if (int.TryParse(input, out i))
            return i;
        double d;
        if (double.TryParse(input, out d))
            return d;
        bool b;
        if (bool.TryParse(input, out b))
            return b;
        if (input.ElementAt(0) == '[')
            return Unserialize(input);

        if (input.StartsWith(@"""")) {
            input = input.Remove(input.Length - 1, 1);
            input = input.Remove(0, 1);
        }

        return input;
    }
}
