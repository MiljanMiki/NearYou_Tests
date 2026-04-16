import { test, expect } from '@playwright/test';

//1.
test.beforeEach ('Login',async({page}) => {
  await page.goto('');

  await page.getByRole('link', { name: 'Prijavite se' }).click();

  await page.getByPlaceholder('Enter username').fill('UsernameTest');
  await page.getByPlaceholder('Enter password').fill('TestTest123!');

   await page.getByRole('button',{name:'Login'}).click();
  

});

//2.
test ('Narucivanje oglasa',async({page}) => {
  
  const Oglas = page.locator('.oglas-card').nth(3); 
  const naslovOglasa = await Oglas.locator('h3').textContent();

  await Oglas.click();

  await page.getByRole("button", {name:"Naruči uslugu"}).click();

 // await page.getByPlaceholder('Npr. Zdravo, zanima me dostupnost za sledeću nedelju... ').fill('Pozdrav zeleo bih da narucim ovaj oglas');
  await page.locator("textarea").fill('Pozdrav zeleo bih da narucim ovaj oglas');
  await page.getByRole("button", {name:"Pošalji"}).click();

     page.on('dialog', async dialog => {
    console.log(`Pojavio se dijalog sa porukom: ${dialog.message()}`);
    await dialog.accept(); 
    await page.getByRole("button", {name:"Otkaži"}).click();
  
  });

  const closeBtn = page.locator('.close-btn');
  if (await closeBtn.isVisible()) {
      await closeBtn.click();
  }

  await page.getByTitle('Moje prijave').click();

  const prijava = page.locator('.prijava-card').filter({hasText:naslovOglasa?? ""}).first();
  await expect(prijava.locator('h3')).toContainText(naslovOglasa ?? "");

});



//3.
test ('Postavi oglas',async({page}) => {

  
  await page.getByRole('link', { name: 'Postavi oglas' }).click();

  const naslovOglasa = "Vodoinstalater";
  await page.getByPlaceholder(/ Električar/i ).fill(naslovOglasa);
  await page.getByPlaceholder("Detaljan opis oglasa").fill("Sve za vodu popravljam...");
  
  await page.locator('input').nth(1).fill("Nis"); // Grad
  await page.locator('input').nth(2).fill("Niska 44"); // Adresa
  await page.locator('.map-picker').click();

   const marker = page.locator('.leaflet-marker-icon'); 
   await expect(marker).toBeVisible();


  await page.locator('input[type="number"]').fill("2000");//Cena

  await page.locator('select').last().selectOption({ index: 4 });//Kategorija- Usluge


  const putanjaDoSlike = 'tests/test-slika.png'; 
  await page.setInputFiles('input[type="file"]', putanjaDoSlike);
  await expect(page.locator('.preview')).toBeVisible();

  await page.getByRole('button', { name: 'Objavi oglas' }).click();
  

  await expect(page).toHaveURL('/');

  await page.getByTitle('Moji oglasi').click();

  const oglas = page.locator('.oglas-card').filter({hasText:naslovOglasa?? ""}).first();
  await expect(oglas.locator('h3')).toContainText(naslovOglasa ?? "");
});


//4.
test ('Brisanje oglasa',async({page}) => {

 
  await page.getByTitle('Moji oglasi').click();
  const naslovOglasa = "Vodoinstalater";
  const oglas = page.locator('.oglas-card').filter({hasText:naslovOglasa?? ""}).first();

  page.on('dialog', async dialog => {
    console.log(`Pojavio se dijalog sa porukom: ${dialog.message()}`);
    await dialog.accept(); // Ovo je "OK"
  });

  await oglas.getByRole("button",{name:"Obriši"}).click();
  //await expect(page.locator('.oglas-card').filter({ hasText: naslovOglasa })).not.toBeVisible();

});

//5.
test ('Izmena profila',async({page}) => {

  
    await page.getByRole('link', { name: 'Moj Profil' }).click();

});