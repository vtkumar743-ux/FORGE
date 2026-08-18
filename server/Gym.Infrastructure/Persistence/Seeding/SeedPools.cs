namespace Gym.Infrastructure.Persistence.Seeding;

/// <summary>
/// Name and copy pools for the demo dataset. Bengaluru-based because the three seeded
/// branches are Koramangala / Indiranagar / Whitefield, so localities, pincodes and
/// pricing bands all read as one real business rather than generic filler.
/// </summary>
internal static class SeedPools
{
    public static readonly string[] MaleFirstNames =
    {
        "Aarav", "Vivaan", "Aditya", "Arjun", "Rohan", "Karthik", "Siddharth", "Nikhil", "Rahul", "Vikram",
        "Ananth", "Praveen", "Manish", "Harsha", "Sandeep", "Naveen", "Gaurav", "Tarun", "Abhishek", "Kunal",
        "Yash", "Devraj", "Suhas", "Girish", "Bharath", "Sathish", "Prateek", "Anirudh", "Chirag", "Varun",
        "Imran", "Faisal", "Zaid", "Joseph", "Ashwin", "Deepak", "Mohit", "Nitin", "Rakesh", "Sameer"
    };

    public static readonly string[] FemaleFirstNames =
    {
        "Ananya", "Diya", "Aadhya", "Meera", "Sneha", "Divya", "Kavya", "Pooja", "Shruti", "Nandini",
        "Lakshmi", "Priya", "Anjali", "Swathi", "Deepika", "Rashmi", "Ishita", "Neha", "Ritu", "Sowmya",
        "Vaishnavi", "Chaitra", "Bhavana", "Trisha", "Aishwarya", "Madhuri", "Sanjana", "Harini", "Preethi", "Namrata",
        "Fatima", "Ayesha", "Sarah", "Grace", "Tanvi", "Aparna", "Keerthi", "Manasa", "Nikita", "Shalini"
    };

    public static readonly string[] LastNames =
    {
        "Sharma", "Verma", "Reddy", "Naidu", "Iyer", "Nair", "Menon", "Rao", "Gowda", "Shetty",
        "Hegde", "Kulkarni", "Deshpande", "Joshi", "Patil", "Bhat", "Kamath", "Pai", "Prabhu", "Murthy",
        "Krishnan", "Subramanian", "Chandrasekhar", "Balakrishnan", "Venkatesh", "Mehta", "Shah", "Kapoor", "Malhotra", "Chopra",
        "Singh", "Chauhan", "Yadav", "Mishra", "Tiwari", "Banerjee", "Chatterjee", "Das", "Bose", "Ghosh",
        "Khan", "Sheikh", "Ansari", "Fernandes", "D'Souza", "Thomas", "Mathew", "Pillai", "Kurup", "Varghese"
    };

    /// <summary>Locality + pincode pairs used for member addresses around the three branches.</summary>
    public static readonly (string Locality, string Pincode)[] Localities =
    {
        ("Koramangala 5th Block", "560095"), ("Koramangala 8th Block", "560095"), ("Ejipura", "560047"),
        ("HSR Layout Sector 2", "560102"), ("BTM Layout 2nd Stage", "560076"), ("Jayanagar 4th Block", "560011"),
        ("Indiranagar 1st Stage", "560038"), ("Domlur", "560071"), ("Ulsoor", "560008"),
        ("CV Raman Nagar", "560093"), ("Kodihalli", "560008"), ("Old Airport Road", "560017"),
        ("Whitefield Main Road", "560066"), ("Brookefield", "560037"), ("Kundalahalli", "560037"),
        ("Varthur Road", "560087"), ("Marathahalli", "560037"), ("Hoodi", "560048")
    };

    public static readonly string[] Goals =
    {
        "Fat loss", "Build muscle", "Strength — first 100 kg squat", "Marathon prep",
        "Post-injury rebuild", "General fitness", "Improve mobility", "Powerlifting meet prep",
        "Cricket season conditioning", "Reduce lower-back pain", "Wedding in 6 months", "Lower blood pressure"
    };

    public static readonly string[] MemberTagPool =
    {
        "vip", "pt-client", "corporate", "student", "referral-source", "off-peak", "long-tenure", "returning"
    };

    public static readonly string[] Occupations =
    {
        "Software engineer", "Product manager", "Chartered accountant", "Doctor", "Teacher",
        "Sales manager", "Designer", "Consultant", "Entrepreneur", "Data analyst"
    };
}
