using Gym.Core.Entities;
using Gym.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Gym.Infrastructure.Persistence.Seeding;

/// <summary>
/// Default content for every public page. This is the CMS-first contract: nothing on the
/// public site is hardcoded in the client, so the owner can rewrite any headline, swap any
/// image and reorder any section without a developer. Copy is deliberately specific
/// (equipment brands, real timings, named localities) rather than aspirational filler.
/// </summary>
internal static class CmsSeed
{
    public static async Task SeedAsync(GymDbContext db, List<Branch> branches, DateOnly today, CancellationToken ct)
    {
        await SiteSettingsAsync(db, ct);
        await FaqsAsync(db, ct);
        await TestimonialsAsync(db, branches, ct);
        await TransformationsAsync(db, branches, ct);
        await BlogAsync(db, today, ct);
        await PagesAsync(db, branches, ct);
    }

    // ---------------------------------------------------------------- site settings

    private static async Task SiteSettingsAsync(GymDbContext db, CancellationToken ct)
    {
        if (await db.SiteSettings.AnyAsync(ct)) return;

        var settings = new List<SiteSetting>
        {
            S("brand.name", "FORGE", "Brand", "Brand name", "text", 1),
            S("brand.tagline", "Strength, built in Bengaluru.", "Brand", "Tagline", "text", 2),
            S("brand.logoUrl", "/media/brand/forge-wordmark.svg", "Brand", "Logo (light on dark)", "image", 3),
            S("brand.logoMarkUrl", "/media/brand/forge-mark.svg", "Brand", "Logo mark", "image", 4),
            S("brand.faviconUrl", "/favicon.svg", "Brand", "Favicon", "image", 5),

            S("theme.ink", "#0A0A0A", "Theme", "Page base", "color", 10),
            S("theme.carbon", "#121212", "Theme", "Raised surface", "color", 11),
            S("theme.steel", "#1E1E1E", "Theme", "Borders & inputs", "color", 12),
            S("theme.smoke", "#9CA3AF", "Theme", "Secondary text", "color", 13),
            S("theme.bone", "#F5F3EE", "Theme", "Primary text", "color", 14),
            S("theme.accent", "#ECD06F", "Theme", "Accent — actions only", "color", 15),
            S("theme.accentHot", "#E8442E", "Theme", "Signal red", "color", 16),
            S("theme.success", "#3ECF8E", "Theme", "Success", "color", 17),

            S("announcement.enabled", "true", "Announcement bar", "Show the bar", "boolean", 20),
            S("announcement.text", "Monsoon Reset — 25% off 3-month and longer memberships. Ends 7 September.", "Announcement bar", "Text", "text", 21),
            S("announcement.linkLabel", "See plans", "Announcement bar", "Link label", "text", 22),
            S("announcement.linkUrl", "/plans", "Announcement bar", "Link URL", "url", 23),

            S("contact.whatsapp", "+919148120500", "Contact", "WhatsApp number", "text", 30),
            S("contact.phone", "+918041729500", "Contact", "Primary phone", "text", 31),
            S("contact.email", "hello@forgestrength.in", "Contact", "General email", "text", 32),
            S("contact.salesEmail", "join@forgestrength.in", "Contact", "Membership email", "text", 33),
            S("contact.whatsappPrefill", "Hi FORGE — I'd like to book a free trial.", "Contact", "WhatsApp pre-filled message", "text", 34),

            S("social.instagram", "https://instagram.com/forgestrength", "Social", "Instagram", "url", 40),
            S("social.youtube", "https://youtube.com/@forgestrength", "Social", "YouTube", "url", 41),
            S("social.linkedin", "https://linkedin.com/company/forgestrength", "Social", "LinkedIn", "url", 42),

            S("seo.titleSuffix", " · FORGE Bengaluru", "SEO", "Title suffix", "text", 50),
            S("seo.defaultDescription", "Three strength-first gyms in Bengaluru — Koramangala, Indiranagar, Whitefield. Eleiko platforms, coached classes, live floor occupancy. Book a free trial.", "SEO", "Default meta description", "textarea", 51),
            S("seo.defaultOgImage", "/media/og/forge-default.jpg", "SEO", "Default OG image", "image", 52),
            S("seo.organisationName", "FORGE Strength & Performance", "SEO", "Legal / schema name", "text", 53),

            S("legal.gstin", "29AABCF4521K1ZP", "Legal", "GSTIN", "text", 60),
            S("legal.registeredName", "Forge Strength & Performance Pvt Ltd", "Legal", "Registered name", "text", 61),
            S("legal.registeredAddress", "1042, 80 Feet Road, 5th Block, Koramangala, Bengaluru 560095", "Legal", "Registered address", "textarea", 62),

            S("app.playStoreUrl", "https://play.google.com/store/apps/details?id=in.forgestrength.app", "App", "Play Store URL", "url", 70),
            S("app.appStoreUrl", "https://apps.apple.com/in/app/forge-strength/id0000000000", "App", "App Store URL", "url", 71),

            S("features.liveOccupancy", "true", "Features", "Show live occupancy meter publicly", "boolean", 80),
            S("features.onlineJoining", "true", "Features", "Allow buying a membership online", "boolean", 81),
            S("features.leaderboard", "true", "Features", "Branch leaderboard (opt-in members only)", "boolean", 82)
        };

        db.SiteSettings.AddRange(settings);
        await db.SaveChangesAsync(ct);
    }

    private static SiteSetting S(string key, string value, string group, string label, string type, int order) =>
        new() { Key = key, Value = value, Group = group, Label = label, ValueType = type, DisplayOrder = order, IsPublic = true };

    // ---------------------------------------------------------------- faqs

    private static async Task FaqsAsync(GymDbContext db, CancellationToken ct)
    {
        if (await db.FaqItems.AnyAsync(ct)) return;

        var faqs = new (string Category, string Q, string A)[]
        {
            ("Membership", "Is there a joining fee?",
                "There is a ₹1,500 admission fee on the monthly plan only. It is waived on every plan of three months or longer, including the off-peak tier."),
            ("Membership", "Can I train at more than one branch?",
                "The Annual All-Access plan covers all three branches. Monthly, quarterly and half-yearly plans are tied to your home branch — you can switch home branch once a quarter at no cost, or add all-branch access for ₹600 a month."),
            ("Membership", "Can I pause my membership?",
                "Yes. Freeze days are included with every plan — 7 on monthly, 14 on quarterly, 30 on half-yearly and 45 on annual. Request a freeze from the app; it starts the day you ask and no fee applies within your allowance."),
            ("Membership", "What happens if I want to cancel?",
                "New memberships carry a 7-day money-back window, no questions asked. After that, monthly plans stop at the end of the current cycle. Fixed-term plans can be transferred to another person once."),
            ("Membership", "Do you have a trial?",
                "One full day, free, including any class on the timetable. Book it online and bring a photo ID. No card details needed."),
            ("Training", "I have never lifted before. Where do I start?",
                "Strength Foundations. It is a 45-minute coached class capped at 16 people covering the squat, hinge, press and pull. Most new members do it twice a week for their first month before moving to the open floor."),
            ("Training", "Do I get a program?",
                "Every member on a quarterly plan or longer gets a written program in the app, updated each training block. Monthly members can buy a one-off programming session for ₹1,200."),
            ("Training", "Are the classes suitable if I am returning from injury?",
                "Tell us at sign-up and we will route you to Divya at Indiranagar, who trained as a physiotherapist. She writes return-to-training plans and clears you into group classes when you are ready."),
            ("Facilities", "Which equipment do you have?",
                "Eleiko platforms and bars, Rogue racks, Keiser M3i bikes in the spin rooms, Concept2 rowers and ski-ergs, and a full dumbbell run to 50 kg. Whitefield adds a 60-metre turf lane and an ice bath."),
            ("Facilities", "How busy does it get?",
                "Weekday evenings between 6.30 and 8.30 are the peak. The live occupancy meter on each branch page shows the floor right now, plus the typical busy hours for that day, so you can pick a quieter window."),
            ("Facilities", "Is there parking?",
                "Whitefield has mall parking with member validation. Koramangala has 18 two-wheeler and 6 four-wheeler spots. Indiranagar has two-wheeler parking only — the 100 Feet Road metro entrance is a 4-minute walk."),
            ("Payments", "Which payment methods do you accept?",
                "UPI, credit and debit cards, net banking and Razorpay payment links online. At the desk we also take cash and cheque, and can split a plan into instalments on request."),
            ("Payments", "Is GST included in the price shown?",
                "Yes. Every price on this site is what you pay, inclusive of 5% GST on gym services. Your invoice shows the tax break-up and SAC code, and is available in the app immediately."),
            ("Payments", "Do you offer corporate rates?",
                "Yes, 15% off for companies with five or more enrolled employees, and an HR usage report each month. Email join@forgestrength.in with your company name to get a code.")
        };

        var i = 0;
        db.FaqItems.AddRange(faqs.Select(f => new FaqItem
        {
            Category = f.Category, Question = f.Q, Answer = f.A, DisplayOrder = ++i
        }));
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- social proof

    private static async Task TestimonialsAsync(GymDbContext db, List<Branch> branches, CancellationToken ct)
    {
        if (await db.Testimonials.AnyAsync(ct)) return;

        var rows = new (string Name, string Role, string Quote, string BranchSlug, string Program, bool Featured)[]
        {
            ("Nandini Rao", "Product manager, 34", "I had a gym membership for four years and used it maybe thirty times. Here the coach noticed I had stopped coming and messaged me. That is the entire difference.", "koramangala", "Strength Foundations", true),
            ("Sathish Kumar", "Chartered accountant, 41", "I came in with a bad lower back and a doctor's note. Nine months later I deadlifted 140 kilos. Nobody rushed me and nobody let me be lazy either.", "whitefield", "Powerlifting Club", true),
            ("Aisha Fernandes", "Designer, 28", "The 6 AM class is full of people who are also not morning people. Sneha somehow makes forty-five minutes on a rower feel like a decision I made willingly.", "koramangala", "HIIT 45", true),
            ("Vikram Malhotra", "Sales director, 45", "Three branches matter more than I expected. Weekdays at Indiranagar, Saturdays at Whitefield with my son. One membership, no arguments at the desk.", "indiranagar", "Annual All-Access", false),
            ("Keerthi Prabhu", "Doctor, 31", "The InBody reports actually get explained. Priya sat with me for twenty minutes and told me which number to ignore, which was more useful than the number itself.", "whitefield", "Body recomposition", false),
            ("Rahul Chauhan", "Software engineer, 26", "I moved here from a chain gym where the racks were always taken. The occupancy meter told me 2 PM was empty, and it was right.", "koramangala", "Off-Peak", false),
            ("Meera Krishnan", "Teacher, 38", "Aparna's Sunday yoga class is the only appointment I do not move. The room is warm, the sequence is hard, and nobody chants at you.", "whitefield", "Yoga Flow", false),
            ("Faisal Ansari", "Entrepreneur, 33", "Imran taught me to keep my hands up before he taught me to hit anything. Six months in, I spar twice a week and my resting heart rate dropped eleven points.", "koramangala", "Boxing Fundamentals", false)
        };

        var bySlug = branches.ToDictionary(b => b.Slug, b => b.Id);
        var order = 0;
        db.Testimonials.AddRange(rows.Select(r => new Testimonial
        {
            AuthorName = r.Name, AuthorRole = r.Role, Quote = r.Quote,
            BranchId = bySlug[r.BranchSlug], Program = r.Program,
            IsFeatured = r.Featured, Rating = 5, DisplayOrder = ++order,
            AuthorPhotoUrl = $"/media/testimonials/{CatalogueSeed.Slugify(r.Name)}.jpg"
        }));
        await db.SaveChangesAsync(ct);
    }

    private static async Task TransformationsAsync(GymDbContext db, List<Branch> branches, CancellationToken ct)
    {
        if (await db.Transformations.AnyAsync(ct)) return;

        var rows = new (string Name, int Weeks, string Program, string Trainer, decimal Before, decimal After, string BranchSlug, string Story)[]
        {
            ("Arjun M.", 24, "Strength Foundations → Powerlifting Club", "Karthik Reddy", 94.5m, 81.2m, "koramangala",
                "Arjun's brief was simple: stop being out of breath on the stairs at work. Twenty-four weeks of three sessions a week, no supplements beyond whey, and a 120 kg squat he did not expect."),
            ("Divya S.", 20, "HIIT 45 + Nutrition coaching", "Priya Deshpande", 78.0m, 66.4m, "whitefield",
                "Two young kids and a full-time job meant four 45-minute windows a week and nothing longer. We built the entire plan around that constraint instead of pretending otherwise."),
            ("Sandeep K.", 36, "Powerlifting Club", "Bharath Gowda", 71.0m, 79.8m, "whitefield",
                "The rarer direction: Sandeep wanted to add size. Nine months, a deliberate 250-calorie surplus and a 60 kg jump on his total."),
            ("Anjali R.", 16, "Strength Foundations", "Sneha Iyer", 68.5m, 62.1m, "koramangala",
                "Anjali had never held a barbell. Sixteen weeks later she pulled 90 kg off the floor and posted it herself, which is how most of the gym found out."),
            ("Praveen T.", 28, "CrossTrain Circuit + Mobility", "Divya Nair", 88.0m, 77.5m, "indiranagar",
                "Came in after a slipped-disc diagnosis and a year of avoiding exercise entirely. Started with loaded carries and breathing drills; finished the block running 5 km without pain.")
        };

        var bySlug = branches.ToDictionary(b => b.Slug, b => b.Id);
        var order = 0;
        db.Transformations.AddRange(rows.Select(r => new Transformation
        {
            MemberDisplayName = r.Name, DurationWeeks = r.Weeks, Program = r.Program, TrainerName = r.Trainer,
            WeightBeforeKg = r.Before, WeightAfterKg = r.After, BranchId = bySlug[r.BranchSlug], Story = r.Story,
            BeforeImageUrl = $"/media/transformations/{CatalogueSeed.Slugify(r.Name)}-before.jpg",
            AfterImageUrl = $"/media/transformations/{CatalogueSeed.Slugify(r.Name)}-after.jpg",
            ConsentGiven = true, ConsentAtUtc = DateTime.UtcNow.AddDays(-90), DisplayOrder = ++order
        }));
        await db.SaveChangesAsync(ct);
    }

    private static async Task BlogAsync(GymDbContext db, DateOnly today, CancellationToken ct)
    {
        if (await db.BlogPosts.AnyAsync(ct)) return;

        var rows = new (string Title, string Excerpt, string Author, string Role, string Tags, int Read, int DaysAgo, bool Featured, string[] Paragraphs)[]
        {
            ("How many days a week should you actually train?",
             "Four is the number most people can hold for a year. Here is why we stop recommending six.",
             "Karthik Reddy", "Head of Strength", "training,programming", 6, 4, true,
             new[]
             {
                 "The honest answer is: as many as you can repeat in twelve months without resenting it. In our own attendance data, members who commit to six days a week average 2.8 actual visits by month three. Members who commit to four average 3.4. The lower target produces more training.",
                 "Four sessions gives you two lower-body days and two upper-body days, or three full-body days and one conditioning slot. Both work. What does not work is a plan that assumes a version of your week that does not exist.",
                 "If you are training for a specific meet or event, this changes — talk to a coach and we will build the block around the date. For everyone else, pick four fixed slots, put them in your calendar as appointments, and protect them for eight weeks before you reconsider."
             }),
            ("A week of Indian meals at 140 g of protein",
             "Paneer, dal, curd and eggs will get you there without a single scoop of powder. A full seven-day template.",
             "Priya Deshpande", "Nutrition & Body Composition", "nutrition,indian-food", 9, 12, true,
             new[]
             {
                 "The most common objection we hear is that hitting a protein target on a vegetarian Indian diet is impractical. It is not — it just requires the protein to be planned first and the carbohydrate to fill in around it, rather than the reverse.",
                 "A working template for a 70 kg member: two eggs or 150 g paneer at breakfast, 200 g curd mid-morning, a cup of cooked dal and 100 g of soya or chicken at lunch, 30 g whey after training, and 150 g paneer or two rotis with rajma at dinner. That lands between 135 and 150 g depending on portion size.",
                 "Weigh things for one week only. The point is not to weigh food forever — it is to learn what 150 g of paneer looks like on your plate so that you never need the scale again."
             }),
            ("Why we put an occupancy meter on the website",
             "Our busiest hour is 7.30 PM and everyone knows it. Publishing the number was still the right call.",
             "Sneha Iyer", "Conditioning Lead", "gym-life,product", 4, 21, false,
             new[]
             {
                 "Most gyms hide how crowded they are. We publish it live on every branch page, derived straight from check-ins, and we publish the typical busy hours for each day alongside it.",
                 "The reason is simple: the alternative is a member driving over at 7.30 PM, finding every rack occupied, and quietly deciding the membership is not worth it. Better that they see Peak, come at 9 PM, and have a good session.",
                 "Since we shipped it, off-peak usage between 10 AM and 4 PM is up 31%, and the off-peak plan has become our third most popular. Being honest about the crowd turned out to be a product feature."
             }),
            ("Your first month at FORGE, session by session",
             "What we will ask you to do in weeks one to four, and what we will deliberately not ask you to do.",
             "Karthik Reddy", "Head of Strength", "beginners,onboarding", 7, 33, false,
             new[]
             {
                 "Week one is two Strength Foundations classes and one walk-through of the floor with a coach. You will lift an empty bar and possibly nothing heavier. This is intentional.",
                 "Week two adds a third session and load on the squat and hinge. Week three we film a set of each so you can see what the coach sees. Week four we set your first working weights and hand over a written program.",
                 "What we will not do in month one: test your maximum on anything, put you on a machine circuit for forty minutes, or sell you a supplement. If any of that happens, tell the manager."
             }),
            ("Ice baths: what the evidence actually supports",
             "Useful for perceived recovery and a genuinely good habit. Not useful immediately after a hypertrophy session.",
             "Divya Nair", "Movement & Mobility", "recovery,evidence", 5, 47, false,
             new[]
             {
                 "Cold-water immersion reliably reduces soreness and improves how recovered people feel. That is a real effect and worth something, particularly in a week with back-to-back hard sessions.",
                 "The caveat is timing. Immersion in the two to four hours after a resistance session blunts some of the signalling that drives muscle growth. If your goal that day was hypertrophy, take the bath the following morning instead.",
                 "For conditioning blocks, tournament weekends and general stress management, use it freely. The Whitefield suite runs at 11°C and four minutes is plenty."
             })
        };

        db.BlogPosts.AddRange(rows.Select(r =>
        {
            var slug = CatalogueSeed.Slugify(r.Title);
            var body = string.Join(",", r.Paragraphs.Select(p =>
                $"{{\"type\":\"paragraph\",\"text\":{System.Text.Json.JsonSerializer.Serialize(p)}}}"));
            return new BlogPost
            {
                Slug = slug, Title = r.Title, Excerpt = r.Excerpt,
                BodyJson = $"[{body}]",
                CoverImageUrl = $"/media/blog/{slug}.jpg",
                AuthorName = r.Author, AuthorRole = r.Role, Tags = r.Tags, ReadMinutes = r.Read,
                SeoTitle = r.Title, SeoDescription = r.Excerpt, OgImageUrl = $"/media/blog/{slug}.jpg",
                State = PublishState.Published,
                PublishedAtUtc = today.AddDays(-r.DaysAgo).ToDateTime(new TimeOnly(9, 30)).AddMinutes(-330),
                IsFeatured = r.Featured,
                ViewCount = 400 - r.DaysAgo * 3
            };
        }));
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- pages & sections

    private static async Task PagesAsync(GymDbContext db, List<Branch> branches, CancellationToken ct)
    {
        if (await db.CmsPages.AnyAsync(ct)) return;

        var pages = new List<CmsPage>
        {
            Page("home", "Home", "FORGE — Strength, built in Bengaluru",
                "Three strength-first gyms in Bengaluru. Eleiko platforms, coached classes, live floor occupancy. Book a free trial at Koramangala, Indiranagar or Whitefield.", 1,
                HomeSections()),

            Page("classes", "Classes & Timetable", "Class timetable — FORGE Bengaluru",
                "Ten class formats across three Bengaluru branches, from Strength Foundations to the Olympic Lifting Lab. Filter by branch, format, trainer or time. Free trial on every class.", 2,
                ClassesSections()),

            Page("trainers", "Trainers", "Our coaches — FORGE Bengaluru",
                "Eight full-time coaches: national-level powerlifters, a physiotherapist, an IWF-certified weightlifting coach and a 900-hour Ashtanga teacher. Book a personal-training session.", 3,
                TrainersSections()),

            Page("plans", "Plans & Pricing", "Membership plans & pricing — FORGE Bengaluru",
                "Monthly from ₹3,200, annual all-access at ₹19,900, and an off-peak tier at ₹1,900. All prices include GST. No joining fee on three months or longer.", 4,
                PricingSections()),

            Page("transformations", "Transformations", "Member transformations — FORGE Bengaluru",
                "Real members, real timelines, published with consent. Before and after, the program that got them there and the coach who wrote it.", 5,
                TransformationSections()),

            Page("journal", "Journal", "The FORGE Journal — training, nutrition and gym life",
                "Programming, Indian nutrition templates and what we have learned running three gyms in Bengaluru. Written by the coaches on the floor.", 6,
                JournalSections()),

            Page("branches", "Branches", "Our three Bengaluru branches — FORGE",
                "Koramangala, Indiranagar and Whitefield. Timings, amenities, live occupancy and per-branch timetables.", 7,
                BranchIndexSections()),

            Page("free-trial", "Book a Free Trial", "Book a free trial — FORGE Bengaluru",
                "One full day free, including any class on the timetable. Pick a branch and a time; we confirm on WhatsApp within the hour.", 8,
                FreeTrialSections()),

            Page("contact", "Contact", "Contact FORGE Bengaluru",
                "Call, WhatsApp or walk in. Addresses, timings and direct numbers for all three branches.", 9,
                ContactSections()),

            Page("faq", "Questions", "Frequently asked questions — FORGE Bengaluru",
                "Joining fees, freezing a membership, multi-branch access, GST on invoices, parking and what to expect in your first month.", 10,
                FaqSections()),

            Page("tools", "Calculators", "BMI & calorie calculators — FORGE Bengaluru",
                "Work out your BMI and daily calorie target, then see which plan and class schedule fits what you are actually trying to do.", 11,
                ToolsSections())
        };

        // One CMS page per branch, so branch copy and SEO are owner-editable per location.
        foreach (var branch in branches)
            pages.Add(Page($"branches/{branch.Slug}", branch.Name,
                $"{branch.Name} — timings, timetable & live occupancy",
                $"{branch.ShortPitch} {branch.AddressLine1}, {branch.City} {branch.Pincode}. Open from {branch.WeekdayOpen:HH\\:mm} on weekdays.",
                20 + branch.DisplayOrder,
                BranchSections(branch),
                structuredData: BranchSchema(branch)));

        db.CmsPages.AddRange(pages);
        await db.SaveChangesAsync(ct);
    }

    private static CmsPage Page(string slug, string title, string seoTitle, string seoDescription,
        int order, List<CmsSection> sections, string? structuredData = null) => new()
    {
        Slug = slug, Title = title, SeoTitle = seoTitle, SeoDescription = seoDescription,
        OgImageUrl = $"/media/og/{slug.Replace('/', '-')}.jpg",
        State = PublishState.Published, PublishedAtUtc = DateTime.UtcNow,
        IsSystemPage = true, DisplayOrder = order, Sections = sections,
        StructuredDataJson = structuredData
    };

    private static CmsSection Section(CmsSectionType type, string key, string label, int order, string json, int? branchId = null) => new()
    {
        SectionType = type, Key = key, AdminLabel = label, OrderIndex = order,
        ContentJson = json.Trim(), State = PublishState.Published, PublishedAtUtc = DateTime.UtcNow,
        IsVisible = true, BranchId = branchId
    };

    private static List<CmsSection> HomeSections() => new()
    {
        Section(CmsSectionType.Hero, "hero", "Home — video hero", 1, """
        {
          "eyebrow": "Koramangala · Indiranagar · Whitefield",
          "headline": "Strength is a\nlong game.",
          "kineticWords": ["Strength", "long game"],
          "subhead": "Three floors in Bengaluru built around barbells, coaching and the unglamorous work of turning up. No contracts, no hard sell, no chrome.",
          "videoUrl": "/media/hero/forge-hero.mp4",
          "videoWebmUrl": "/media/hero/forge-hero.webm",
          "posterUrl": "/media/hero/forge-hero-poster.jpg",
          "posterAlt": "A lifter setting up for a heavy deadlift on a chalked Eleiko platform, low warm light from the side",
          "overlayOpacity": 0.62,
          "primaryCta": { "label": "Book a free trial", "href": "/free-trial" },
          "secondaryCta": { "label": "Book a tour", "href": "/contact" },
          "tertiaryCta": { "label": "See plans", "href": "/plans" },
          "scrollHint": "Scroll"
        }
        """),

        Section(CmsSectionType.MarqueeTicker, "ticker-disciplines", "Home — discipline ticker", 2, """
        {
          "items": ["Strength", "Olympic lifting", "Conditioning", "Boxing", "Spin", "Mobility", "Recovery", "Powerlifting"],
          "separator": "—",
          "speedSeconds": 42,
          "style": "outline",
          "direction": "left"
        }
        """),

        Section(CmsSectionType.Manifesto, "manifesto", "Home — manifesto block", 3, """
        {
          "eyebrow": "What we are",
          "headline": "A gym for people who intend to still be lifting in ten years.",
          "body": "We opened in Koramangala in 2019 with four platforms and one rule: coach the lift before you load it. Six years and three branches later that is still the whole method. Our members are engineers, doctors, accountants and students who train three or four times a week for decades, not for eight-week challenges.",
          "signatureLine": "Karthik Reddy, Head of Strength",
          "imageUrl": "/media/facility/coaching-floor.jpg",
          "imageAlt": "A coach kneeling beside a member setting up for a squat, correcting bar position",
          "imagePosition": "right"
        }
        """),

        Section(CmsSectionType.StatBand, "stats", "Home — animated stat band", 4, """
        {
          "eyebrow": "The floor, in numbers",
          "stats": [
            { "value": 3, "suffix": "", "label": "Branches across Bengaluru", "format": "integer" },
            { "value": 40, "suffix": "", "label": "Coached classes every week", "format": "integer" },
            { "value": 8, "suffix": "", "label": "Full-time coaches, no freelancers", "format": "integer" },
            { "value": 2100, "suffix": "+", "label": "Members training right now", "format": "integer" },
            { "value": 13, "suffix": "", "label": "Eleiko platforms network-wide", "format": "integer" }
          ],
          "footnote": "Membership figure updated monthly. Platform count includes the fourth Koramangala platform commissioned this month."
        }
        """),

        Section(CmsSectionType.AmenityBento, "amenities", "Home — amenity bento grid", 5, """
        {
          "eyebrow": "Inside",
          "headline": "Everything on the floor, named.",
          "tiles": [
            { "size": "2x2", "title": "The strength deck", "body": "Thirteen Eleiko platforms and Rogue racks across three branches, with calibrated plates and competition bars on every platform.", "imageUrl": "/media/facility/strength-deck.jpg", "imageAlt": "A row of platforms with loaded barbells under warm overhead light", "iconKey": "barbell" },
            { "size": "1x1", "title": "Ice-bath recovery suite", "body": "11°C, four-minute protocol. Whitefield.", "imageUrl": "/media/facility/ice-bath.jpg", "imageAlt": "A stainless ice bath in a dark tiled recovery room", "iconKey": "snowflake" },
            { "size": "1x1", "title": "Keiser M3i spin rooms", "body": "24 bikes at Koramangala, 28 at Indiranagar. Watts on screen.", "imageUrl": "/media/facility/spin-room.jpg", "imageAlt": "A dark spin studio lit red, bikes in formation", "iconKey": "bike" },
            { "size": "2x1", "title": "60-metre turf lane", "body": "Sled pushes, prowler work and sprint mechanics without going outside into Bengaluru traffic. Whitefield only.", "imageUrl": "/media/facility/turf-lane.jpg", "imageAlt": "An indoor turf lane with a loaded sled at one end", "iconKey": "run" },
            { "size": "1x2", "title": "Combat zone", "body": "Twelve bags, two rings of floor space and wraps at the desk. Coached pad rounds six days a week.", "imageUrl": "/media/facility/combat-zone.jpg", "imageAlt": "Heavy bags hanging in a dark room with chalk-dusted floor", "iconKey": "boxing-glove" },
            { "size": "1x1", "title": "InBody 570", "body": "Composition scans at every branch, read back to you by a coach.", "imageUrl": "/media/facility/inbody.jpg", "imageAlt": "An InBody body-composition scanner beside a consultation desk", "iconKey": "body-scan" },
            { "size": "1x1", "title": "Open 5 AM – 11 PM", "body": "Weekdays. Weekend hours vary by branch.", "iconKey": "clock" }
          ]
        }
        """),

        Section(CmsSectionType.ClassRail, "class-rail", "Home — class format rail", 6, """
        {
          "eyebrow": "Classes",
          "headline": "Ten formats. Every one coached by a full-timer.",
          "body": "Capped class sizes so a coach can actually correct your fourth rep. Book from the app up to 72 hours ahead; the waitlist promotes automatically when someone drops.",
          "formatSlugs": ["strength-foundations", "hiit-45", "olympic-lifting-lab", "boxing-fundamentals", "spin-room", "mobility-recovery"],
          "showCapacityRing": true,
          "showSpotsLeft": true,
          "cta": { "label": "See the full timetable", "href": "/classes" }
        }
        """),

        Section(CmsSectionType.SignatureScroll, "signature", "Home — signature scroll moment", 7, """
        {
          "headline": "Turn up.",
          "subline": "The rest is arithmetic.",
          "imageUrl": "/media/facility/wide-floor.jpg",
          "imageAlt": "Wide view of the Whitefield strength floor at 6 AM, empty platforms lit from above",
          "startScale": 0.8,
          "endScale": 1,
          "letterTightening": true
        }
        """),

        Section(CmsSectionType.TrainerHighlight, "trainers", "Home — trainer highlights", 8, """
        {
          "eyebrow": "The coaches",
          "headline": "Eight people who do this full time.",
          "body": "No rotating freelancers and no commission on supplement sales. Every coach here holds an internationally recognised certification and works one floor, so they know your name and your working weights.",
          "trainerSlugs": ["karthik-reddy", "divya-nair", "bharath-gowda", "sneha-iyer"],
          "duotoneOnHover": true,
          "cta": { "label": "Meet all eight", "href": "/trainers" }
        }
        """),

        Section(CmsSectionType.TransformationSlider, "transformations", "Home — transformation slider", 9, """
        {
          "eyebrow": "Twelve to thirty-six weeks",
          "headline": "Published with consent, timelines included.",
          "body": "We do not run eight-week challenges and we do not publish anything without written permission. Each card shows the actual duration and the program that produced it.",
          "limit": 3,
          "showWeights": true,
          "cta": { "label": "All transformations", "href": "/transformations" }
        }
        """),

        Section(CmsSectionType.TestimonialWall, "testimonials", "Home — testimonial wall", 10, """
        {
          "eyebrow": "Members",
          "headline": "In their words.",
          "featuredOnly": true,
          "limit": 3,
          "showGoogleRating": true,
          "googleRating": 4.8,
          "googleReviewCount": 612,
          "googleReviewUrl": "https://g.page/forgestrength-koramangala"
        }
        """),

        Section(CmsSectionType.AppQr, "app", "Home — app & QR section", 11, """
        {
          "eyebrow": "The app",
          "headline": "Book, scan in, log the set.",
          "body": "One-tap booking with the waitlist handled for you, a QR that opens the turnstile, your program with a rest timer, and a personal-record banner that goes off when you beat your own number.",
          "bullets": ["Live branch occupancy before you leave home", "Waitlist auto-promotion with a push", "Set logging with rest timer and PR detection", "InBody history and progress photo compare", "Invoices, renewals and freeze requests"],
          "qrCaption": "Scan to install",
          "screenshotUrl": "/media/app/forge-app-home.png",
          "screenshotAlt": "The FORGE member app home screen showing today's booked class, a streak counter and branch occupancy"
        }
        """),

        Section(CmsSectionType.CtaBanner, "cta", "Home — closing CTA", 12, """
        {
          "headline": "One free day. Any class on the timetable.",
          "body": "Bring a photo ID and shoes you can lift in. We will show you the floor, put you through a session and leave you alone about signing up.",
          "primaryCta": { "label": "Book a free trial", "href": "/free-trial" },
          "secondaryCta": { "label": "WhatsApp us", "href": "whatsapp" },
          "imageUrl": "/media/facility/chalk-hands.jpg",
          "imageAlt": "Chalked hands gripping a barbell at the start of a set",
          "microcopy": "No card details. No obligation. We reply within the hour, 6 AM to 10 PM."
        }
        """)
    };

    private static List<CmsSection> ClassesSections() => new()
    {
        Section(CmsSectionType.Hero, "hero", "Classes — page hero", 1, """
        {
          "variant": "compact",
          "eyebrow": "Timetable",
          "headline": "Forty classes a week.",
          "subhead": "Filter by branch, format, coach or time of day. Booking opens 72 hours ahead and the waitlist promotes itself when a spot frees up.",
          "posterUrl": "/media/classes/timetable-hero.jpg",
          "posterAlt": "A conditioning class mid-interval, rowers and kettlebells on a studio floor",
          "overlayOpacity": 0.7,
          "primaryCta": { "label": "Book a free trial", "href": "/free-trial" }
        }
        """),

        Section(CmsSectionType.TimetableEmbed, "timetable", "Classes — live timetable", 2, """
        {
          "defaultBranchSlug": "koramangala",
          "daysVisible": 7,
          "filters": ["branch", "format", "trainer", "timeOfDay", "level"],
          "showSpotsLeft": true,
          "showCapacityRing": true,
          "trialCtaLabel": "Book free trial",
          "emptyState": "No classes match those filters. Try clearing the time-of-day filter, or switch branch — Whitefield runs the widest weekend timetable."
        }
        """),

        Section(CmsSectionType.ClassRail, "formats", "Classes — all formats", 3, """
        {
          "eyebrow": "The formats",
          "headline": "What each class actually involves.",
          "formatSlugs": [],
          "showAll": true,
          "showCapacityRing": false,
          "layout": "grid"
        }
        """),

        Section(CmsSectionType.FaqAccordion, "faq", "Classes — training FAQs", 4, """
        {
          "eyebrow": "Before you book",
          "headline": "Common questions about the classes.",
          "categories": ["Training"],
          "limit": 4
        }
        """)
    };

    private static List<CmsSection> TrainersSections() => new()
    {
        Section(CmsSectionType.Hero, "hero", "Trainers — page hero", 1, """
        {
          "variant": "compact",
          "eyebrow": "Coaching",
          "headline": "Eight coaches.\nNo freelancers.",
          "subhead": "Between them: two NSCA-CSCS, an IWF Level 1, a practising physiotherapy degree, 900 hours of Ashtanga training in Mysuru and a 24-fight amateur boxing record.",
          "posterUrl": "/media/trainers/team-hero.jpg",
          "posterAlt": "A coach spotting a member through a heavy set of bench press",
          "overlayOpacity": 0.68,
          "primaryCta": { "label": "Book a PT session", "href": "/free-trial?intent=pt" }
        }
        """),

        Section(CmsSectionType.TrainerHighlight, "roster", "Trainers — full roster", 2, """
        {
          "eyebrow": "The roster",
          "headline": "Who you will train with.",
          "trainerSlugs": [],
          "showAll": true,
          "duotoneOnHover": true,
          "showRatings": true,
          "showPtPrice": true,
          "groupByBranch": true
        }
        """),

        Section(CmsSectionType.CtaBanner, "cta", "Trainers — PT CTA", 3, """
        {
          "headline": "Twelve sessions, one coach, a written program.",
          "body": "Personal training runs at ₹18,000 for twelve sixty-minute sessions, valid 120 days at any branch. Includes programming between sessions and form-check video review.",
          "primaryCta": { "label": "Enquire about PT", "href": "/free-trial?intent=pt" },
          "secondaryCta": { "label": "See plans", "href": "/plans" },
          "imageUrl": "/media/facility/pt-session.jpg",
          "imageAlt": "A coach and member reviewing a lift on a phone beside a rack",
          "microcopy": "Requires an active membership · Sessions transferable within family"
        }
        """)
    };

    private static List<CmsSection> PricingSections() => new()
    {
        Section(CmsSectionType.Hero, "hero", "Plans — page hero", 1, """
        {
          "variant": "compact",
          "eyebrow": "Membership",
          "headline": "Four plans. One price each.",
          "subhead": "Every figure below includes GST and is what you actually pay. No joining fee on three months or longer, and a 7-day money-back window on new memberships.",
          "posterUrl": "/media/facility/rack-detail.jpg",
          "posterAlt": "Close detail of a loaded barbell resting in a rack, chalk on the knurling",
          "overlayOpacity": 0.72
        }
        """),

        Section(CmsSectionType.OfferBanner, "offer", "Plans — seasonal offer banner", 2, """
        {
          "enabled": true,
          "couponCode": "MONSOON25",
          "headline": "Monsoon Reset — 25% off three months and longer",
          "body": "Applies to quarterly, half-yearly and annual plans at all three branches. Ends 7 September.",
          "cta": { "label": "Apply at checkout", "href": "#plans" },
          "urgencyStyle": "countdown"
        }
        """),

        Section(CmsSectionType.PricingTable, "plans", "Plans — pricing table", 3, """
        {
          "eyebrow": "Plans",
          "headline": "Pick the length you will actually see through.",
          "highlightedPlanSlugs": ["quarterly", "annual-all-access", "monthly"],
          "comparePlanSlugs": ["monthly", "quarterly", "half-yearly", "annual-all-access", "off-peak"],
          "cycleToggle": ["monthly", "quarterly", "half-yearly", "annual"],
          "defaultCycle": "annual",
          "showBranchSelector": true,
          "trustMicrocopy": "No joining fee on 3 months+ · Pause any plan · 7-day money-back on new memberships · GST included",
          "compareRows": [
            { "label": "Gym floor access", "key": "floor" },
            { "label": "All group classes", "key": "classes" },
            { "label": "Branches included", "key": "branches" },
            { "label": "Freeze days", "key": "freeze" },
            { "label": "Guest passes", "key": "guests" },
            { "label": "InBody scans", "key": "scans" },
            { "label": "Written program", "key": "program" },
            { "label": "PT sessions included", "key": "pt" }
          ],
          "footnote": "The off-peak tier covers 10 AM to 4 PM, Monday to Saturday. Personal training is sold separately as a 12-session pack and requires an active membership."
        }
        """),

        Section(CmsSectionType.FaqAccordion, "faq", "Plans — billing FAQs", 4, """
        {
          "eyebrow": "The details",
          "headline": "Fees, freezing and refunds.",
          "categories": ["Membership", "Payments"],
          "limit": 9
        }
        """),

        Section(CmsSectionType.CtaBanner, "cta", "Plans — closing CTA", 5, """
        {
          "headline": "Try it before you pay for it.",
          "body": "A free day gets you the floor, a class and a conversation with a coach about which plan makes sense. Most people change their mind at least once.",
          "primaryCta": { "label": "Book a free trial", "href": "/free-trial" },
          "secondaryCta": { "label": "Join online now", "href": "/plans#checkout" },
          "microcopy": "UPI, card, net banking or a payment link on WhatsApp."
        }
        """)
    };

    private static List<CmsSection> TransformationSections() => new()
    {
        Section(CmsSectionType.Hero, "hero", "Transformations — page hero", 1, """
        {
          "variant": "compact",
          "eyebrow": "Members",
          "headline": "Sixteen weeks.\nThirty-six weeks.",
          "subhead": "Every card below shows the real duration, the program that produced it and the coach who wrote it. Published only with written consent.",
          "posterUrl": "/media/transformations/hero.jpg",
          "posterAlt": "A member mid-set on a heavy squat, chalk dust in the light",
          "overlayOpacity": 0.7
        }
        """),

        Section(CmsSectionType.TransformationSlider, "gallery", "Transformations — full gallery", 2, """
        {
          "showAll": true,
          "layout": "grid",
          "showWeights": true,
          "showStory": true,
          "sliderHandleLabel": "Drag to compare"
        }
        """),

        Section(CmsSectionType.TestimonialWall, "testimonials", "Transformations — testimonials", 3, """
        {
          "eyebrow": "Members",
          "headline": "What the timeline felt like from the inside.",
          "featuredOnly": false,
          "limit": 6,
          "layout": "masonry"
        }
        """),

        Section(CmsSectionType.CtaBanner, "cta", "Transformations — CTA", 4, """
        {
          "headline": "Start the twelve weeks.",
          "body": "Book a free day, get an InBody scan and leave with a coach's honest read on what your timeline actually looks like.",
          "primaryCta": { "label": "Book a free trial", "href": "/free-trial" },
          "secondaryCta": { "label": "Talk to a coach", "href": "/contact" }
        }
        """)
    };

    private static List<CmsSection> JournalSections() => new()
    {
        Section(CmsSectionType.Hero, "hero", "Journal — page hero", 1, """
        {
          "variant": "text",
          "eyebrow": "Journal",
          "headline": "Written by the coaches on the floor.",
          "subhead": "Programming, Indian nutrition templates and what six years of running three gyms has taught us. No listicles."
        }
        """),

        Section(CmsSectionType.BlogRail, "posts", "Journal — post list", 2, """
        {
          "layout": "featured-plus-grid",
          "featuredCount": 2,
          "pageSize": 9,
          "showTags": true,
          "showReadTime": true,
          "showAuthor": true,
          "emptyState": "Nothing published yet. The coaches are on the floor."
        }
        """)
    };

    private static List<CmsSection> BranchIndexSections() => new()
    {
        Section(CmsSectionType.Hero, "hero", "Branches — page hero", 1, """
        {
          "variant": "compact",
          "eyebrow": "Locations",
          "headline": "Three floors.\nOne membership.",
          "subhead": "Koramangala for the strength deck, Indiranagar for the studios, Whitefield for space and the ice bath. Annual All-Access covers all three.",
          "posterUrl": "/media/branches/index-hero.jpg",
          "posterAlt": "Exterior of the Koramangala branch at dusk, warm light through tall windows",
          "overlayOpacity": 0.68
        }
        """),

        Section(CmsSectionType.BranchLocator, "locator", "Branches — locator & occupancy", 2, """
        {
          "eyebrow": "Find your floor",
          "headline": "Pick the one you will actually get to.",
          "showLiveOccupancy": true,
          "showMap": true,
          "showTimings": true,
          "showAmenityChips": true,
          "sortBy": "displayOrder",
          "nearestFirstPrompt": "Use my location to sort by distance"
        }
        """),

        Section(CmsSectionType.CtaBanner, "cta", "Branches — CTA", 3, """
        {
          "headline": "Not sure which one?",
          "body": "Tell us where you live or work and which hours you can train, and we will tell you honestly which branch to pick — including if the answer is none of them.",
          "primaryCta": { "label": "Book a tour", "href": "/contact" },
          "secondaryCta": { "label": "WhatsApp us", "href": "whatsapp" }
        }
        """)
    };

    private static List<CmsSection> BranchSections(Branch branch) => new()
    {
        Section(CmsSectionType.Hero, $"hero", $"{branch.Name} — hero", 1, $$"""
        {
          "variant": "branch",
          "eyebrow": "{{branch.City}} · {{branch.AddressLine1.Split(',').Last().Trim()}}",
          "headline": {{Json(branch.Name.Replace("FORGE ", string.Empty))}},
          "subhead": {{Json(branch.ShortPitch ?? string.Empty)}},
          "posterUrl": "{{branch.HeroImageUrl}}",
          "posterAlt": "Interior of the {{branch.Name}} training floor",
          "overlayOpacity": 0.66,
          "primaryCta": { "label": "Book a free trial", "href": "/free-trial?branch={{branch.Slug}}" },
          "secondaryCta": { "label": "Book a tour", "href": "/contact?branch={{branch.Slug}}" }
        }
        """, branch.Id),

        Section(CmsSectionType.AmenityBento, "amenities", $"{branch.Name} — amenities", 2, BranchAmenityJson(branch), branch.Id),

        Section(CmsSectionType.AnnotatedFacility, "facility", $"{branch.Name} — annotated floor plan", 3, BranchAnnotationJson(branch), branch.Id),

        Section(CmsSectionType.TimetableEmbed, "timetable", $"{branch.Name} — timetable", 4, $$"""
        {
          "defaultBranchSlug": "{{branch.Slug}}",
          "lockBranch": true,
          "daysVisible": 7,
          "filters": ["format", "trainer", "timeOfDay"],
          "showSpotsLeft": true,
          "showCapacityRing": true,
          "trialCtaLabel": "Book free trial",
          "emptyState": "Nothing scheduled for this filter at {{branch.Name}}."
        }
        """, branch.Id),

        Section(CmsSectionType.ContactBlock, "contact", $"{branch.Name} — contact & map", 5, $$"""
        {
          "headline": "Getting here",
          "addressLines": [{{Json(branch.AddressLine1)}}, {{Json(branch.AddressLine2 ?? string.Empty)}}, {{Json($"{branch.City} {branch.Pincode}")}}],
          "phone": "{{branch.Phone}}",
          "whatsapp": "{{branch.WhatsAppNumber}}",
          "email": "{{branch.Email}}",
          "mapUrl": "{{branch.GoogleMapsUrl}}",
          "latitude": {{branch.Latitude}},
          "longitude": {{branch.Longitude}},
          "timings": [
            { "label": "Monday to Friday", "value": "{{branch.WeekdayOpen:HH\:mm}} – {{branch.WeekdayClose:HH\:mm}}" },
            { "label": "Saturday & Sunday", "value": "{{branch.WeekendOpen:HH\:mm}} – {{branch.WeekendClose:HH\:mm}}" }
          ],
          "showLiveOccupancy": true,
          "parkingNote": {{Json(ParkingNote(branch.Slug))}}
        }
        """, branch.Id),

        Section(CmsSectionType.CtaBanner, "cta", $"{branch.Name} — CTA", 6, $$"""
        {
          "headline": "One free day at {{branch.Name.Replace("FORGE ", string.Empty)}}.",
          "body": "Any class on the timetable, the full floor and a coach walking you through it. Bring a photo ID.",
          "primaryCta": { "label": "Book a free trial", "href": "/free-trial?branch={{branch.Slug}}" },
          "secondaryCta": { "label": "Call the branch", "href": "tel:{{branch.Phone}}" },
          "microcopy": "We confirm on WhatsApp within the hour, 6 AM to 10 PM."
        }
        """, branch.Id)
    };

    private static string ParkingNote(string slug) => slug switch
    {
        "koramangala" => "18 two-wheeler and 6 four-wheeler spots in the basement. Street parking on 80 Feet Road is enforced after 8 AM.",
        "indiranagar" => "Two-wheeler parking only. Indiranagar metro entrance is a 4-minute walk; we validate two hours of mall parking next door on weekends.",
        _ => "Mall parking with member validation — bring your ticket to the desk. Level 2 is closest to the entrance."
    };

    private static string BranchAmenityJson(Branch branch)
    {
        var tiles = branch.Slug switch
        {
            "koramangala" => """
                { "size": "2x2", "title": "Four Eleiko platforms", "body": "Calibrated plates, competition bars and a coach on the deck from 5 AM.", "imageUrl": "/media/facility/kor-platforms.jpg", "imageAlt": "Four lifting platforms in a row under warm light", "iconKey": "barbell" },
                { "size": "1x1", "title": "24-bike spin room", "body": "Keiser M3i, watts on screen.", "imageUrl": "/media/facility/spin-room.jpg", "imageAlt": "Dark spin studio lit red", "iconKey": "bike" },
                { "size": "1x1", "title": "Combat zone", "body": "Twelve bags, wraps at the desk.", "imageUrl": "/media/facility/combat-zone.jpg", "imageAlt": "Heavy bags in a dark room", "iconKey": "boxing-glove" },
                { "size": "2x1", "title": "Recovery suite", "body": "Sauna, compression boots and a physio room used by Divya on Tuesdays and Fridays.", "imageUrl": "/media/facility/recovery-suite.jpg", "imageAlt": "A quiet recovery room with a sauna door and compression chairs", "iconKey": "snowflake" },
                { "size": "1x1", "title": "Open 5 AM – 11 PM", "body": "Weekdays. 6 AM – 10 PM weekends.", "iconKey": "clock" }
                """,
            "indiranagar" => """
                { "size": "2x2", "title": "Two glass studios", "body": "Daylight on three sides, sprung floors and the shortest average class wait in the network.", "imageUrl": "/media/facility/ind-studio.jpg", "imageAlt": "A bright glass-walled studio with mats laid out", "iconKey": "studio" },
                { "size": "1x1", "title": "28-bike spin room", "body": "Our largest spin floor.", "imageUrl": "/media/facility/spin-room.jpg", "imageAlt": "Dark spin studio lit red", "iconKey": "bike" },
                { "size": "1x1", "title": "Physio-led rehab", "body": "Divya's return-to-training clinic.", "imageUrl": "/media/facility/rehab.jpg", "imageAlt": "A treatment table beside banded rehab equipment", "iconKey": "stretch" },
                { "size": "2x1", "title": "Strength floor", "body": "Three platforms, six racks and a full dumbbell run to 50 kg.", "imageUrl": "/media/facility/strength-deck.jpg", "imageAlt": "Racks and platforms on a strength floor", "iconKey": "barbell" },
                { "size": "1x1", "title": "4 min from metro", "body": "Indiranagar station, 100 Feet Road exit.", "iconKey": "train" }
                """,
            _ => """
                { "size": "2x2", "title": "Six platforms, 22,000 sq ft", "body": "Our largest floor. Six Eleiko platforms, ten racks and enough space that nobody queues at 7 PM.", "imageUrl": "/media/facility/whf-floor.jpg", "imageAlt": "A very large strength floor with platforms along one wall", "iconKey": "barbell" },
                { "size": "1x1", "title": "Ice bath, 11°C", "body": "Four-minute protocol, towels provided.", "imageUrl": "/media/facility/ice-bath.jpg", "imageAlt": "A stainless ice bath in a dark tiled room", "iconKey": "snowflake" },
                { "size": "1x1", "title": "60-metre turf lane", "body": "Sleds, prowlers and sprint work.", "imageUrl": "/media/facility/turf-lane.jpg", "imageAlt": "Indoor turf lane with a loaded sled", "iconKey": "run" },
                { "size": "2x1", "title": "Recovery suite", "body": "Sauna, ice bath, compression boots and a quiet room with no music.", "imageUrl": "/media/facility/recovery-suite.jpg", "imageAlt": "A recovery room with sauna and compression chairs", "iconKey": "spa" },
                { "size": "1x1", "title": "Mall parking", "body": "Validated at the desk.", "iconKey": "car" },
                { "size": "1x1", "title": "Open 5 AM – 11.30 PM", "body": "The longest hours in the network.", "iconKey": "clock" }
                """
        };

        return $$"""
        {
          "eyebrow": "Inside {{branch.Name.Replace("FORGE ", string.Empty)}}",
          "headline": "{{branch.FloorAreaSqft:N0}} square feet, itemised.",
          "tiles": [{{tiles}}]
        }
        """;
    }

    private static string BranchAnnotationJson(Branch branch)
    {
        var callouts = branch.Slug switch
        {
            "koramangala" => """
                { "x": 22, "y": 34, "label": "Eleiko platforms 1–4", "detail": "Calibrated plates, 20 kg competition bars" },
                { "x": 58, "y": 26, "label": "Rack row", "detail": "Six Rogue R-3 racks with safety arms" },
                { "x": 74, "y": 62, "label": "Conditioning corner", "detail": "Concept2 rowers, ski-ergs, assault bikes" },
                { "x": 34, "y": 74, "label": "Recovery suite", "detail": "Sauna and compression, past the studio door" }
                """,
            "indiranagar" => """
                { "x": 26, "y": 30, "label": "Studio One", "detail": "Sprung floor, daylight on three sides" },
                { "x": 62, "y": 38, "label": "Spin room", "detail": "28 Keiser M3i bikes, output on screen" },
                { "x": 44, "y": 68, "label": "Strength floor", "detail": "Three platforms, dumbbells to 50 kg" },
                { "x": 78, "y": 72, "label": "Rehab room", "detail": "Physio-led return-to-training clinic" }
                """,
            _ => """
                { "x": 18, "y": 40, "label": "Platforms 1–6", "detail": "Six Eleiko platforms along the north wall" },
                { "x": 48, "y": 24, "label": "Turf lane", "detail": "60 m, sleds and prowlers at the far end" },
                { "x": 70, "y": 52, "label": "Ice-bath suite", "detail": "11°C, four-minute protocol" },
                { "x": 36, "y": 76, "label": "Studio One", "detail": "26-person capacity, full mirror wall" }
                """
        };

        return $$"""
        {
          "eyebrow": "The floor",
          "headline": "Where everything is.",
          "imageUrl": "/media/facility/{{branch.Slug}}-floorplan.jpg",
          "imageAlt": "Wide interior photograph of the {{branch.Name}} floor with equipment zones visible",
          "leaderLineColor": "accent",
          "callouts": [{{callouts}}]
        }
        """;
    }

    private static List<CmsSection> FreeTrialSections() => new()
    {
        Section(CmsSectionType.Hero, "hero", "Free trial — page hero", 1, """
        {
          "variant": "split",
          "eyebrow": "Free trial",
          "headline": "One day. Everything open.",
          "subhead": "Pick a branch and a rough time. We confirm on WhatsApp within the hour and hold a coach for the first fifteen minutes so you are not left staring at a rack.",
          "posterUrl": "/media/facility/trial-hero.jpg",
          "posterAlt": "A coach greeting a new member at the front desk",
          "overlayOpacity": 0.6,
          "bullets": ["Any class on that day's timetable", "Full floor and recovery suite access", "InBody scan if you want one", "No card details, no obligation"]
        }
        """),

        Section(CmsSectionType.LeadForm, "form", "Free trial — booking form", 2, """
        {
          "headline": "Two steps, about forty seconds.",
          "steps": [
            {
              "title": "Who you are",
              "fields": [
                { "name": "fullName", "label": "Full name", "type": "text", "required": true, "autoComplete": "name" },
                { "name": "phone", "label": "Mobile number", "type": "tel", "required": true, "autoComplete": "tel", "help": "We confirm on WhatsApp — no calls unless you ask." },
                { "name": "email", "label": "Email", "type": "email", "required": false, "autoComplete": "email" }
              ]
            },
            {
              "title": "When and where",
              "fields": [
                { "name": "branchSlug", "label": "Branch", "type": "branch", "required": true },
                { "name": "trialDate", "label": "Preferred date", "type": "date", "required": true },
                { "name": "preferredTime", "label": "Time of day", "type": "select", "required": true, "options": ["Early morning (5–8 AM)", "Morning (8–11 AM)", "Midday (11 AM–4 PM)", "Evening (4–7 PM)", "Late evening (7–10 PM)"] },
                { "name": "goal", "label": "What are you training for?", "type": "select", "required": false, "options": ["Fat loss", "Build muscle", "Get stronger", "General fitness", "Return from injury", "Event or race prep"] },
                { "name": "message", "label": "Anything we should know?", "type": "textarea", "required": false, "help": "Injuries, medical conditions, or if you have never trained before." }
              ]
            }
          ],
          "consentLabel": "Send me class updates and offers on WhatsApp. You can stop this any time by replying STOP.",
          "submitLabel": "Book my free day",
          "successHeadline": "Booked. Check WhatsApp.",
          "successBody": "You will get a confirmation on WhatsApp within the hour, with the coach's name and what to bring. If it is outside 6 AM to 10 PM, first thing in the morning.",
          "failureBody": "That did not go through. Call the branch directly on +91 80 4172 9500 and we will book you in manually.",
          "intent": "trial"
        }
        """),

        Section(CmsSectionType.FaqAccordion, "faq", "Free trial — FAQs", 3, """
        {
          "eyebrow": "Before you come",
          "headline": "What to expect.",
          "categories": ["Training", "Facilities"],
          "limit": 5
        }
        """)
    };

    private static List<CmsSection> ContactSections() => new()
    {
        Section(CmsSectionType.Hero, "hero", "Contact — page hero", 1, """
        {
          "variant": "text",
          "eyebrow": "Contact",
          "headline": "Call, message or walk in.",
          "subhead": "Each branch has its own desk number and WhatsApp line, answered by the people who work that floor."
        }
        """),

        Section(CmsSectionType.BranchLocator, "branches", "Contact — all branch details", 2, """
        {
          "eyebrow": "Branches",
          "headline": "Three desks, three numbers.",
          "showLiveOccupancy": true,
          "showMap": true,
          "showTimings": true,
          "showPhone": true,
          "showWhatsApp": true,
          "showEmail": true,
          "layout": "list"
        }
        """),

        Section(CmsSectionType.ContactBlock, "general", "Contact — head office & enquiries", 3, """
        {
          "headline": "Anything else",
          "rows": [
            { "label": "Membership enquiries", "value": "join@forgestrength.in", "type": "email" },
            { "label": "Corporate memberships", "value": "join@forgestrength.in", "type": "email", "note": "Five or more employees — 15% off plus a monthly HR usage report." },
            { "label": "Coaching applications", "value": "careers@forgestrength.in", "type": "email", "note": "Send certifications and a two-minute coaching clip." },
            { "label": "Anything gone wrong", "value": "+919148120500", "type": "whatsapp", "note": "Goes to the owner, not a queue." }
          ],
          "responseNote": "We answer WhatsApp between 6 AM and 10 PM, seven days. Email within one working day."
        }
        """)
    };

    private static List<CmsSection> FaqSections() => new()
    {
        Section(CmsSectionType.Hero, "hero", "FAQ — page hero", 1, """
        {
          "variant": "text",
          "eyebrow": "Questions",
          "headline": "The things people actually ask.",
          "subhead": "If it is not here, WhatsApp the branch — a person will answer."
        }
        """),

        Section(CmsSectionType.FaqAccordion, "faq", "FAQ — all questions", 2, """
        {
          "showAll": true,
          "groupByCategory": true,
          "categoryOrder": ["Membership", "Training", "Facilities", "Payments"],
          "defaultOpenFirst": true
        }
        """),

        Section(CmsSectionType.CtaBanner, "cta", "FAQ — CTA", 3, """
        {
          "headline": "Still deciding?",
          "body": "Come and see the floor at the hour you would actually train. That answers more than a price list does.",
          "primaryCta": { "label": "Book a free trial", "href": "/free-trial" },
          "secondaryCta": { "label": "WhatsApp us", "href": "whatsapp" }
        }
        """)
    };

    private static List<CmsSection> ToolsSections() => new()
    {
        Section(CmsSectionType.Hero, "hero", "Tools — page hero", 1, """
        {
          "variant": "text",
          "eyebrow": "Calculators",
          "headline": "Two numbers worth knowing.",
          "subhead": "Both are estimates. Useful as a starting point, not as a verdict — a coach and an InBody scan will tell you far more."
        }
        """),

        Section(CmsSectionType.CalculatorBlock, "bmi", "Tools — BMI calculator", 2, """
        {
          "kind": "bmi",
          "headline": "Body Mass Index",
          "body": "BMI ignores muscle mass entirely, which is why a lot of our stronger members read as overweight on it. Treat it as one crude input.",
          "inputs": [
            { "name": "heightCm", "label": "Height", "unit": "cm", "min": 120, "max": 220, "default": 170 },
            { "name": "weightKg", "label": "Weight", "unit": "kg", "min": 35, "max": 200, "default": 72 }
          ],
          "bands": [
            { "max": 18.5, "label": "Under", "tone": "warn" },
            { "max": 23, "label": "Healthy (Asian-Indian range)", "tone": "success" },
            { "max": 27.5, "label": "Elevated", "tone": "warn" },
            { "max": 999, "label": "High", "tone": "hot" }
          ],
          "footnote": "Bands follow the WHO Asian-Indian cut-offs, which are lower than the international ones.",
          "cta": { "label": "Book an InBody scan instead", "href": "/free-trial" }
        }
        """),

        Section(CmsSectionType.CalculatorBlock, "bmr", "Tools — BMR & calorie calculator", 3, """
        {
          "kind": "bmr",
          "headline": "Daily calorie target",
          "body": "We use the Mifflin-St Jeor equation, then apply an activity multiplier. Eat at maintenance for two weeks and weigh yourself before you trust the number.",
          "inputs": [
            { "name": "sex", "label": "Sex", "type": "select", "options": ["Male", "Female"] },
            { "name": "age", "label": "Age", "unit": "years", "min": 14, "max": 90, "default": 30 },
            { "name": "heightCm", "label": "Height", "unit": "cm", "min": 120, "max": 220, "default": 170 },
            { "name": "weightKg", "label": "Weight", "unit": "kg", "min": 35, "max": 200, "default": 72 },
            { "name": "activity", "label": "Training days per week", "type": "select", "options": ["0–1", "2–3", "4–5", "6–7"] },
            { "name": "goal", "label": "Goal", "type": "select", "options": ["Lose fat", "Maintain", "Build muscle"] }
          ],
          "outputs": ["bmr", "maintenance", "target", "proteinGrams"],
          "footnote": "Protein target is 1.8 g per kg of bodyweight, which is where most of our members do best.",
          "cta": { "label": "See the Indian meal template", "href": "/journal/a-week-of-indian-meals-at-140-g-of-protein" }
        }
        """)
    };

    /// <summary>LocalBusiness/HealthClub JSON-LD per branch (Module 1.12).</summary>
    private static string BranchSchema(Branch branch) => $$"""
    {
      "@context": "https://schema.org",
      "@type": "HealthClub",
      "name": {{Json(branch.Name)}},
      "image": "{{branch.HeroImageUrl}}",
      "telephone": "{{branch.Phone}}",
      "email": "{{branch.Email}}",
      "address": {
        "@type": "PostalAddress",
        "streetAddress": {{Json($"{branch.AddressLine1}, {branch.AddressLine2}")}},
        "addressLocality": "{{branch.City}}",
        "addressRegion": "{{branch.State}}",
        "postalCode": "{{branch.Pincode}}",
        "addressCountry": "IN"
      },
      "geo": { "@type": "GeoCoordinates", "latitude": {{branch.Latitude}}, "longitude": {{branch.Longitude}} },
      "openingHoursSpecification": [
        { "@type": "OpeningHoursSpecification", "dayOfWeek": ["Monday","Tuesday","Wednesday","Thursday","Friday"], "opens": "{{branch.WeekdayOpen:HH\:mm}}", "closes": "{{branch.WeekdayClose:HH\:mm}}" },
        { "@type": "OpeningHoursSpecification", "dayOfWeek": ["Saturday","Sunday"], "opens": "{{branch.WeekendOpen:HH\:mm}}", "closes": "{{branch.WeekendClose:HH\:mm}}" }
      ],
      "priceRange": "₹₹",
      "currenciesAccepted": "INR",
      "paymentAccepted": "UPI, Card, Net banking, Cash"
    }
    """;

    private static string Json(string value) => System.Text.Json.JsonSerializer.Serialize(value);
}
