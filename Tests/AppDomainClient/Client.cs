using System;

class Client
{
    static int Main (string [] args)
    {
        int res = 0;/*c6632437-1cac-45db-ac15-0ca13cf02aa1*/
        foreach (string s in args) {
            res += Convert.ToInt32 (s);
        }
        return res;
    }
}