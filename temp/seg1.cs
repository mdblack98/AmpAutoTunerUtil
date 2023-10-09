using System;

public class Example
{
   public static void Main()
   {
      string guidString = "ba748d5c-ae5f-4cca-84e5-1ac5291c38cb";
      try {
             Console.WriteLine(Guid.ParseExact(guidString, "G"));
      }
      catch (Exception ex)
      {
             Console.WriteLine(ex.StackTrace + "\n" + ex.Message);
	     return;
      }
   }
}