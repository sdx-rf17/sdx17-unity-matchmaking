# Selling requirements — Sdx17 Matchmaking v0.1.0

**Do NOT submit to the Asset Store until every box below is checked.
This file is a local checklist for the publisher (Sdx17), not part of
the listing copy.**

Current status: v0.1.0 is **UNTESTED IN UNITY**. No listing exists yet,
no sales and no reviews have been claimed, and the $35 suggested price
guarantees nothing.

---

## 1. Unity verification (must all pass)

- [ ] Package imports into a **clean Unity 2022.3 LTS project** with
      **zero errors and zero warnings** in the Console. Fix or explain
      every warning — do not submit with warnings you have not read.
- [ ] `Sdx17 > Matchmaking > Create Matchmaking Config` creates the
      asset; the custom inspector shows validation messages.
- [ ] `Sdx17 > Matchmaking > Add Matchmaking Service to Scene` works.
- [ ] Demo scene plays end to end (build it from
      `Samples~/Demo/README.md`): Join Queue → Add Bots → ready-check
      starts → Accept → `MATCH FOUND!` in the Console.
      Note the known demo behaviour: bots never accept a ready-check on
      their own, so verify the full accept flow with the self-test.
- [ ] `MatchmakingSelfTest` attached in a test scene, Play mode, logs
      `DONE: 34 passed, 0 failed` (34 checks, per the README). Do not
      ship with a failing self-test.
- [ ] Decline a ready-check in the demo: the lobby dissolves and the
      accepters are re-queued.
- [ ] Leave the queue running with one player for 60 seconds: the
      queue timeout fires and `OnQueueTimeout` is observed.
- [ ] Re-run the checks above after any code change, and record the
      Unity version used (must be 2022.3 LTS) and the date.

## 2. Docs proofread

- [ ] `README.md` proofread — every class/method name matches the real
      `Runtime/*.cs` files; no invented API; the UNTESTED IN UNITY
      status is still stated honestly.
- [ ] `Documentation~/GUIDE.md` proofread — quickstart steps actually
      work in the clean project; API reference matches the real code;
      FAQ answers match observed behaviour.
- [ ] `LISTING.md` proofread — title, short description under
      200 characters, price, requirements, and version history all
      match the package as submitted. No sales, reviews, or listing
      claimed that does not exist.
- [ ] `CHANGELOG.md` matches the submitted version (0.1.0 only).
- [ ] `LICENSE` (MIT) and `ThirdPartyNotices.txt` present and accurate.

## 3. Screenshots — real Unity screenshots only

- [ ] At least **5 screenshots**, all real screenshots captured from
      Unity (the demo scene and/or the Inspector), at **1920×1080**.
- [ ] Screenshots show the real demo: queue UI, ready-check, lobby /
      match-found state, the `MatchmakingConfig` inspector, and the
      self-test PASS output in the Console (or equivalent real views).
- [ ] **No AI images presented as screenshots.** Concept art is not a
      screenshot. If any image was AI-generated, it must not be shown
      as if it were the product running in Unity.
- [ ] No stand-in, mockup, or mismatched numbers in screenshots
      (no invented player counts, revenue, or user figures).

## 4. Video — real Unity recording only

- [ ] One video, **30–60 seconds**, a real screen recording of Unity
      (not a slideshow, not AI-generated video): import → add service
      → create config → Join Queue → Add Bots → ready-check → match
      found (or the self-test run, where the full accept flow is
      visible).
- [ ] Video has no fake UI, no AI-generated footage presented as the
      product, and no audio claiming features that are not in the
      package.

## 5. Listing assets

- [ ] Icon **512×512** (the Asset Store icon size). Original Sdx17
      artwork — do not use another asset's icon, a stock image you do
      not own, or an AI image presented as a screenshot.
- [ ] **AI-content disclosure:** if any concept art (or any other
      AI-generated image) is used anywhere in the listing, disclose it
      as AI-generated where Unity asks for that disclosure, and never
      present it as a screenshot or as gameplay.
- [ ] Listing text is the proofread `LISTING.md` copy (title
      "Sdx17 Matchmaking", short description under 200 chars).

## 6. Price — re-check before listing

- [ ] Re-check the **$35 suggested price against comparable
      matchmaking / lobby / multiplayer-toolkit assets** on the Asset
      Store immediately before listing. $35 is the top of the
      $15–$35 band a new publisher should use to get first reviews,
      justified only by the ready-check / party / skill-rating scope.
      If comparable, tested assets sell for less, price below them.
- [ ] Confirm the maths: 70% net = **$24.50 per $35 sale before tax
      and refunds**.
- [ ] Do not claim the price guarantees sales. It does not.

## 7. Publisher account — set up before submitting

- [ ] Unity publisher account created and verified.
- [ ] **Two-factor authentication (2FA)** enabled on the publisher
      account.
- [ ] **Tax information** completed in the publisher portal.
- [ ] **PayPal** (payout method) set up and verified.
- [ ] Publisher display name is Sdx17, consistent with the package
      (`package.json` author) and the GitHub profile (sdx-rf17).

## 8. Submission rules — know them before you submit

- [ ] Asset Store **review takes at least 5 business days** — plan
      for it; do not promise buyers a date you cannot control.
- [ ] **5 submissions per month cap in the first 6 months** (new
      publisher limit) — each submission counts, including fixes after
      a rejection. Do not waste submissions on an untested package.
- [ ] Submission package contains only the files that belong in the
      asset (this checklist file, `LISTING.md` and
      `SELLING_REQUIREMENTS.md` are working documents — check Unity's
      submission guidance on whether to include them; when in doubt,
      leave local checklists out of the submitted package).
- [ ] No fake email, no fake reviews, no invented sales history, no
      support contact that does not exist. Support contact is Sdx17 /
      GitHub sdx-rf17 only.

## 9. Final gate (sign off here)

- [ ] All boxes above are checked, with the Unity version and date of
      the clean-project test recorded here:
      Unity version: _______________  Date tested: _______________
      Tested by: Sdx17
- [ ] The honest limitations are still in the listing: UNTESTED IN
      UNITY status (until the tests above pass, then update the status
      to tested with the version/date), mock backend is local-only, a
      real backend is required for launch, and this is not a
      networking layer.

**If any box is unchecked, do not submit.**
