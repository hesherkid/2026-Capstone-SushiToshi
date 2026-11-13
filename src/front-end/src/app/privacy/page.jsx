import { Box, Link, Paper, Typography } from "@mui/material";
import CustomerPageContainer from "@/components/customer/CustomerPageContainer";

export const metadata = {
  title: "Privacy Policy",
  description: "How Sushi Toshi collects and uses your information",
};

// The restaurant must fill these in, and have the wording reviewed, before launch
// (see "Privacy policy" in docs/setup/production-setup.md)
const LAST_UPDATED = "November 12, 2026";
// Privacy law needs someone accountable for privacy; the owner or a manager can be that person
const PRIVACY_CONTACT = {
  title: "Privacy Contact",
  email: "privacy@example.com",
  phone: "000-000-0000",
  address: "Street address, City, Alberta, Postal code",
};

// Only what PIPEDA and Alberta's PIPA expect a privacy policy to tell people
const sections = [
  {
    heading: "What We Collect",
    paragraphs: [
      "If you create an account, we collect your name, your email address and a securely hashed password. If you sign in with Google, we get your name, email address and Google account ID from Google instead. Guest accounts have no name, email address or password.",
      "When you use the App, we record your table, your bill and its guest counts, your orders, your requests to call a server, the menu items you open, and any favourites you save. Your browser also stores your sign-in and your current order on your device.",
    ],
  },
  {
    heading: "Why We Use It",
    paragraphs: [
      "We use this information to take your orders, prepare your bill, respond to your requests, show your favourites and past orders, and send emails you ask for, such as verifying your email address or resetting your password. We also use it to see how our menu and service are doing.",
      "By using the App, you consent to these uses. You can withdraw your consent at any time by contacting us, but some features, such as your account and favourites, may then stop working.",
    ],
  },
  {
    heading: "Who We Share It With",
    paragraphs: [
      "We do not sell your personal information. We share it only with the service providers that help run the App: Twilio SendGrid, which sends our emails, and Google, if you choose to sign in with Google. These providers may store your information outside Canada, including in the United States, where it is subject to the laws of that country. Contact us to learn more about how they handle it.",
    ],
  },
  {
    heading: "How We Keep It Safe",
    paragraphs: [
      "We protect your personal information with reasonable safeguards, and our staff can see only what they need for their job. We keep it only as long as we need it. Guest accounts that are never used to order are deleted automatically after about one day.",
    ],
  },
  {
    heading: "Your Rights",
    paragraphs: [
      "You can ask to see the personal information we hold about you and ask us to correct it. To make a request, contact us using the details below.",
    ],
  },
  {
    heading: "Contact Us",
    paragraphs: [
      "Sushi Toshi's management is responsible for protecting your personal information. For questions, requests or complaints, please contact:",
    ],
    contact: true,
    after:
      "If you are not satisfied with our response, you can contact the Office of the Information and Privacy Commissioner of Alberta or the Office of the Privacy Commissioner of Canada.",
  },
];

const ContactBlock = () => (
  <Paper variant="outlined" sx={{ mt: 2, p: 2 }}>
    <Typography sx={{ fontWeight: 700 }}>{PRIVACY_CONTACT.title}, Sushi Toshi</Typography>
    <Typography sx={{ mt: 1 }}>
      Email: <Link href={`mailto:${PRIVACY_CONTACT.email}`}>{PRIVACY_CONTACT.email}</Link>
    </Typography>
    <Typography>
      Phone: <Link href={`tel:${PRIVACY_CONTACT.phone}`}>{PRIVACY_CONTACT.phone}</Link>
    </Typography>
    <Typography>Address: {PRIVACY_CONTACT.address}</Typography>
  </Paper>
);

// Public: linked from sign-in and sign-up, so it has no RequireRole guard
const PrivacyPage = () => (
  <CustomerPageContainer sx={{ maxWidth: 760, textAlign: "left" }}>
    <Box sx={{ px: { xs: 2, sm: 0 }, py: { xs: 3, sm: 0 } }}>
      <Typography variant="h4" component="h1" sx={{ fontWeight: 700 }}>
        Sushi Toshi App Privacy Policy
      </Typography>
      <Typography color="text.secondary" sx={{ mt: 1 }}>
        Last updated {LAST_UPDATED}
      </Typography>

      <Typography sx={{ mt: 3 }}>
        This policy explains how Sushi Toshi collects, uses and protects your personal information when you use
        our table ordering app (the &quot;App&quot;). It does not cover our website.
      </Typography>

      {sections.map((section, index) => (
        <Box component="section" key={section.heading} sx={{ mt: 4 }}>
          <Typography variant="h6" component="h2" sx={{ fontWeight: 700 }}>
            {index + 1}. {section.heading}
          </Typography>
          {section.paragraphs.map((paragraph) => (
            <Typography key={paragraph} sx={{ mt: 1.5 }}>
              {paragraph}
            </Typography>
          ))}
          {section.contact && <ContactBlock />}
          {section.after && <Typography sx={{ mt: 1.5 }}>{section.after}</Typography>}
        </Box>
      ))}
    </Box>
  </CustomerPageContainer>
);

export default PrivacyPage;
